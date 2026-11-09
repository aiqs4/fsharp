#!/usr/bin/env bash
# orchestrate.sh — Incremental F# compiler build orchestration
#
# Design:
#   - Stamp-based incrementalism: each step is independently re-entrant
#   - Single restore with --no-restore on all downstream steps
#   - Bootstrap tools are pinned via CLI MSBuild props (not env vars) to
#     prevent SIGABRT (exit 134) from Release-built fslex/fsyacc that crash
#     because they link against the still-building in-dev FSharp.Core
#   - BUILDING_USING_DOTNET=true routes all outputs to artifacts/ layout
#   - IgnoreMibc=true skips MIBC package downloads that bloat restore time
#   - Product TFM is resolved from eng/TargetFrameworks.props (single source of truth)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
ARTIFACTS="$REPO_ROOT/artifacts"
STAMPS="$ARTIFACTS/.stamps"
BOOTSTRAP="$ARTIFACTS/Bootstrap"
PROTO_PROJ="$REPO_ROOT/proto.proj"
MAIN_SLN="$REPO_ROOT/FSharp.slnx"

readonly TEST_PROJECTS=(
  "tests/FSharp.Compiler.ComponentTests/FSharp.Compiler.ComponentTests.fsproj"
  "tests/FSharp.Compiler.Service.Tests/FSharp.Compiler.Service.Tests.fsproj"
  "tests/FSharp.Compiler.Private.Scripting.UnitTests/FSharp.Compiler.Private.Scripting.UnitTests.fsproj"
  "tests/FSharp.Build.UnitTests/FSharp.Build.UnitTests.fsproj"
  "tests/FSharp.Core.UnitTests/FSharp.Core.UnitTests.fsproj"
)

# Positional args (exec compat with build.sh)
CONFIGURATION="${1:-Release}"
TARGET_FRAMEWORK="${2:-}"
VERBOSE="${3:-false}"
BINARY_LOG="${4:-false}"
MODE="${5:-build}"
FORCE="${6:-false}"
JOBS="${7:-1}"
STAMP_NAME="${8:-}"

# ── Environment ───────────────────────────────────────────────────────────────
# Make apphosts (bootstrap fsc etc.) resolve the same runtime as the `dotnet`
# on PATH (e.g. an RC SDK installed outside /usr/share/dotnet).
if command -v dotnet >/dev/null 2>&1; then
  DOTNET_BIN="$(readlink -f "$(command -v dotnet)")"
  export DOTNET_ROOT="$(dirname "$DOTNET_BIN")"
fi
export DOTNET_MULTILEVEL_LOOKUP=0
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

# Single source of truth for the product TFM (unless overridden with --tfm)
if [[ -z "$TARGET_FRAMEWORK" ]]; then
  TARGET_FRAMEWORK="$(dotnet msbuild "$REPO_ROOT/eng/TargetFrameworks.props" \
      -getProperty:FSharpNetCoreProductTargetFramework 2>/dev/null | tr -d '[:space:]' || true)"
  TARGET_FRAMEWORK="${TARGET_FRAMEWORK:-net10.0}"
fi

# ── MSBuild property sets ─────────────────────────────────────────────────────
#
# RESTORE_PROPS
#   IgnoreMibc=true
#     Skips CopyMIBCWrapper (AfterTargets=Restore in Directory.Build.targets)
#     which downloads MIBC optimisation packages — the primary source of long
#     local restore times. Not needed for non-official dev builds.
#   BUILDING_USING_DOTNET=true
#     Activates $(ArtifactsPackagesDir) as a NuGet source in FSharpBuild props.
#
# BUILD_PROPS
#   BUILDING_USING_DOTNET=true
#     Routes all project outputs to artifacts/ via the conditional OutputPath /
#     IntermediateOutputPath blocks in Directory.Build.props and each .fsproj.
#     Without it, projects fall back to their local bin/ defaults.
#
#   FSharpTargetsPath=$BOOTSTRAP/fsc/Microsoft.FSharp.Targets  [FILE path]
#     MSBuild CLI global props cannot be overridden by .props/.targets
#     assignments. This forces the SDK (Microsoft.NET.Sdk.FSharp.props line 29)
#     to import bootstrap targets instead of the SDK-bundled ones, making every
#     F# compile step invoke bootstrap fsc.dll — the stable self-compile base.
#     NOTE: This is the FILE, not the directory.
#           (FSHARP_TARGETS_PATH env var is directory-based; we use the MSBuild
#            prop directly for reliability and explicitness.)
#
#   FsLexPath / FsYaccPath → Bootstrap binaries
#     *** PRIMARY FIX FOR EXIT CODE 134 (SIGABRT) ***
#     Directory.Build.targets unconditionally overrides FsLexPath/FsYaccPath
#     to artifacts/bin/fslex/.../fslex.dll when BUILDING_USING_DOTNET=true.
#     The Release-built tools crash at runtime because they were compiled against
#     the in-development FSharp.Core which is still building in parallel when
#     CallFsLex runs for FSharp.Compiler.Service. Bootstrap tools use the shipped
#     FSharp.Core and are always stable. CLI global props win over .targets.
#
#   SKIP_NETCURRENT_FSC_BUILD=true
#     FSharp.Compiler.Service.fsproj multi-targets (netcurrent + product TFM) for
#     non-official/local builds to get BCL nullness annotations. This doubles
#     compiler build time with no functional benefit for local dev. Safe to skip.

RESTORE_PROPS=(
  "/p:BUILDING_USING_DOTNET=true"
  "/p:IgnoreMibc=true"
  "/p:FSharpNetCoreProductTargetFramework=$TARGET_FRAMEWORK"
  "/p:NuGetAudit=false" # TODO: this is just a workaround
)

BUILD_PROPS=(
  "/p:FSharpNetCoreProductTargetFramework=$TARGET_FRAMEWORK"
  "/p:BUILDING_USING_DOTNET=true"
  "/p:FSharpTargetsPath=$BOOTSTRAP/fsc/Microsoft.FSharp.Targets"
  "/p:FsLexPath=$BOOTSTRAP/fslex/fslex.dll"
  "/p:FsYaccPath=$BOOTSTRAP/fsyacc/fsyacc.dll"
  "/p:SKIP_NETCURRENT_FSC_BUILD=true"
)

# ── Logging ───────────────────────────────────────────────────────────────────

log()         { printf '[BUILD] %s\n' "$*" >&2; }
log_section() {
  local bar='━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━'
  printf '\n%s\n %s\n%s\n' "$bar" "$*" "$bar" >&2
}
skip()  { printf '[SKIP]  %s\n' "$*" >&2; }
error() { printf '[ERROR] %s\n' "$*" >&2; exit 1; }

# ── Incremental stamp state ───────────────────────────────────────────────────

stamp_file()     { printf '%s/%s' "$STAMPS" "$1"; }
stamp_touch()    { mkdir -p "$STAMPS" && touch "$(stamp_file "$1")"; }
stamp_clear()    { rm -f "$(stamp_file "$1")"; log "✓ Stamp cleared: $1"; }
stamp_clear_all(){ rm -rf "$STAMPS";            log "✓ All stamps cleared"; }

# stale <stamp_name> <search_root> <name_patterns...>
# Returns 0 (rebuild needed): FORCE=true, stamp missing, or any matching
# file under root is newer than stamp. Prunes obj/, bin/ and artifacts/ subdirs.
stale() {
  local name="$1" root="$2"; shift 2
  [[ "$FORCE" == "true" ]] && return 0
  local s; s="$(stamp_file "$name")"
  [[ ! -f "$s" ]] && return 0

  local -a expr=()
  for pat in "$@"; do
    [[ ${#expr[@]} -gt 0 ]] && expr+=(-o)
    expr+=(-name "$pat")
  done

  find "$root" \( -name "obj" -o -name "bin" -o -name "artifacts" -o -name ".git" -o -name ".dotnet" \) -prune \
    -o -type f \( "${expr[@]}" \) -newer "$s" -print -quit 2>/dev/null \
  | grep -q . && return 0

  return 1
}

# ── dotnet helpers ────────────────────────────────────────────────────────────

verbosity() { [[ "$VERBOSE" == "true" ]] && echo "diagnostic" || echo "minimal"; }

# dn <args...> — appends -bl when BINARY_LOG=true
dn() {
  [[ "$BINARY_LOG" == "true" ]] && set -- "$@" "-bl"
  dotnet "$@"
}

# ── Step: restore ─────────────────────────────────────────────────────────────
# Single restore for the main solution. All downstream steps pass --no-restore.
# Proto/bootstrap has its own embedded restore (separate NuGet graph).

step_restore() {
  stale "restore" "$REPO_ROOT" \
    "*.fsproj" "*.csproj" "*.slnx" "*.sln" \
    "global.json" "NuGet.Config" "NuGet.config" \
    "Directory.Build.props" "Directory.Build.targets" "Directory.Packages.props" \
    "Packages.props" "Versions.props" "Version.Details.props" "TargetFrameworks.props" \
  || { skip "restore (project files unchanged)"; return 0; }

  log_section "Restore"
  dotnet restore "$MAIN_SLN" -v "$(verbosity)" "${RESTORE_PROPS[@]}" \
  || error "Restore failed"
  stamp_touch "restore"
}

# ── Step: bootstrap ───────────────────────────────────────────────────────────
# Uses SDK-bundled fsc + its own embedded restore. Does NOT use BUILD_PROPS.
# Stale check: src/ *.fs/fsproj OR proto.proj at root changed.
#
# OR logic: A || B || { skip }
#   stale A=0 → short-circuits, || block skipped → fall through to rebuild ✓
#   stale A=1 → evaluate B:
#     stale B=0 → short-circuits → rebuild ✓
#     stale B=1 → execute { skip } → return ✓

step_bootstrap() {
  stale "bootstrap" "$REPO_ROOT/src" "*.fsproj" "*.fsi" "*.fs" \
  || stale "bootstrap" "$REPO_ROOT"  "proto.proj" \
  || { skip "bootstrap (proto sources unchanged)"; _validate_bootstrap; return 0; }

  log_section "Bootstrap (Configuration=Proto, $TARGET_FRAMEWORK)"

  local -a bl=(); [[ "$BINARY_LOG" == "true" ]] && bl+=("-bl")
  # BUILDING_USING_DOTNET must be pinned: Directory.Build.props auto-enables it as soon as
  # artifacts/Bootstrap/fsc/fsc.dll exists. In a clean checkout that happens *between* projects
  # (fsc publishes it, fsi is built after), so fsi would be restored with the flag off but built with
  # it on (FSHARPCORE_USE_PACKAGE, output paths, ...) => NETSDK1047. Incremental local builds never
  # see the flip because the file already exists.
  dotnet publish "$PROTO_PROJ" \
    -c Proto \
    /p:BUILDING_USING_DOTNET=false \
    /p:FSharpNetCoreProductTargetFramework="$TARGET_FRAMEWORK" \
    /p:DotNetBuild=false \
    /p:DotNetBuildSourceOnly=false \
    -v "$(verbosity)" \
    ${bl[@]+"${bl[@]}"} \
  || error "Bootstrap failed"

  _validate_bootstrap
  stamp_touch "bootstrap"
}

_validate_bootstrap() {
  local -a required=(
    "$BOOTSTRAP/fsc/fsc.dll"
    "$BOOTSTRAP/fsc/fsc.runtimeconfig.json"
    "$BOOTSTRAP/fsc/Microsoft.FSharp.Targets"
    "$BOOTSTRAP/fsc/Microsoft.FSharp.NetSdk.props"
    "$BOOTSTRAP/fslex/fslex.dll"
    "$BOOTSTRAP/fsyacc/fsyacc.dll"
  )
  for f in "${required[@]}"; do
    [[ -e "$f" ]] || error "Bootstrap missing: $f"
  done
  log "✓ Bootstrap valid"
}

# ── Step: build compiler ──────────────────────────────────────────────────────
# Stale check scoped to src/ — test-only changes don't require a recompile.
# BUILD_PROPS pins fslex/fsyacc to bootstrap (prevents SIGABRT exit 134).
# BUILD_PROPS also enforces artifacts/ output layout and uses bootstrap fsc.

step_build_compiler() {
  stale "compiler" "$REPO_ROOT/src" "*.fs" "*.fsi" "*.fsproj" "*.targets" "*.props" "*.fsl" "*.fsy" "*.txt" "*.resx" \
  || { skip "compiler (src unchanged)"; return 0; }

  log_section "Compiler ($CONFIGURATION / $TARGET_FRAMEWORK)"
  # Product projects only: fsc/fsi pull in FCS, FSharp.Build, DependencyManager and Interactive.Settings.
  # FSharp.Core is listed explicitly: with BUILDING_USING_DOTNET=true the projects consume the *shipped*
  # FSharp.Core package (FSHARPCORE_USE_PACKAGE), so building fsc/fsi does not build FSharp.Core.fsproj, which
  # is itself a deliverable (and one of the NuGet packages).
  # FSharp.slnx also contains every test project; those have their own step (tests-build) and must not be able
  # to fail the compiler/publish pipeline. Set BUILD_FULL_SOLUTION=true to build the whole solution anyway.
  local -a targets=(
    "$REPO_ROOT/src/FSharp.Core/FSharp.Core.fsproj"
    "$REPO_ROOT/src/fsc/fscProject/fsc.fsproj"
    "$REPO_ROOT/src/fsi/fsiProject/fsi.fsproj"
  )
  [[ "${BUILD_FULL_SOLUTION:-false}" == "true" ]] && targets=("$MAIN_SLN")
  local target
  for target in "${targets[@]}"; do
    dn build "$target" \
      -c "$CONFIGURATION" \
      --no-restore \
      -v "$(verbosity)" \
      "${BUILD_PROPS[@]}" \
    || error "Compiler build failed ($(basename "$target"))"
  done

  step_verify_compiler
  stamp_touch "compiler"
}

step_verify_compiler() {
  # FSharp.Core is built for netstandard2.1 (shipped) — not for the product TFM.
  local -a required=(
    "$ARTIFACTS/bin/fsc/$CONFIGURATION/$TARGET_FRAMEWORK/fsc.dll"
    "$ARTIFACTS/bin/FSharp.Core/$CONFIGURATION/net10.0/FSharp.Core.dll"
  )
  for f in "${required[@]}"; do
    [[ -e "$f" ]] || error "Compiler output missing: $f"
  done
  log "✓ Compiler verified"
}

# ── Step: build tests ─────────────────────────────────────────────────────────
# Stale check scoped to tests/ — src/ changes only invalidate the compiler stamp.
# Parallel when JOBS > 1; per-job temp logs dumped on failure.

step_build_tests() {
  stale "tests-built" "$REPO_ROOT/tests" "*.fs" "*.fsi" "*.fsproj" \
  || { skip "tests-build (test sources unchanged)"; return 0; }

  log_section "Build Tests (j=$JOBS)"

  local -a pids=() projs=()
  local tmp; tmp="$(mktemp -d)"
  # shellcheck disable=SC2064
  trap "rm -rf '$tmp'" RETURN

  for proj in "${TEST_PROJECTS[@]}"; do
    local full="$REPO_ROOT/$proj"
    [[ -f "$full" ]] || continue

    if [[ "$JOBS" -gt 1 ]]; then
      dotnet build "$full" \
        -c "$CONFIGURATION" --no-restore -v "$(verbosity)" \
        "${BUILD_PROPS[@]}" \
        >"$tmp/$(basename "$full" .fsproj).log" 2>&1 &
      pids+=($!); projs+=("$proj")
    else
      log "Building $(basename "$proj")..."
      dotnet build "$full" \
        -c "$CONFIGURATION" --no-restore -v "$(verbosity)" \
        "${BUILD_PROPS[@]}" \
      || error "Test build failed: $proj"
    fi
  done

  if [[ ${#pids[@]} -gt 0 ]]; then
    local -a failed=()
    for i in "${!pids[@]}"; do
      wait "${pids[$i]}" \
        || { failed+=("${projs[$i]}"); cat "$tmp/$(basename "${projs[$i]}" .fsproj).log" >&2; }
    done
    [[ ${#failed[@]} -gt 0 ]] && error "Test build failed: ${failed[*]}"
  fi

  log "✓ Test projects built"
  stamp_touch "tests-built"
}

# ── Step: run tests ───────────────────────────────────────────────────────────

step_run_tests() {
  log_section "Run Tests ($CONFIGURATION / $TARGET_FRAMEWORK)"

  local count=0
  local -a failed=()

  for proj in "${TEST_PROJECTS[@]}"; do
    local full="$REPO_ROOT/$proj"
    [[ -f "$full" ]] || continue
    count=$((count + 1))     # NOT ((count++)): returns 1 when count==0 and trips `set -e`
    log "▶ $(basename "$proj" .fsproj)"

    local -a args=(
      "test" "--project" "$full"
      "-c" "$CONFIGURATION"
      "--no-build"
      "${BUILD_PROPS[@]}"
    )
    [[ "$VERBOSE" == "true" ]] && args+=("--verbosity" "detailed")

    dotnet "${args[@]}" || failed+=("$proj")
  done

  log "── Summary: $count project(s) ──"
  if [[ ${#failed[@]} -eq 0 ]]; then
    log "✓ All tests passed"
  else
    log "✗ ${#failed[@]} failed:"
    printf '  - %s\n' "${failed[@]}" >&2
    return 1
  fi
}

# ── Step: publish executables ─────────────────────────────────────────────────

step_publish_exe() {
  log_section "Publish Executables (fsc / fsi)"
  local out="$ARTIFACTS/bin/exe"
  mkdir -p "$out" "$ARTIFACTS/log"

  declare -A tools=(
    [fsc]="src/fsc/fscProject/fsc.fsproj"
    [fsi]="src/fsi/fsiProject/fsi.fsproj"
  )

  for tool in "${!tools[@]}"; do
    log "Publishing $tool..."
    local -a bl=()
    [[ "$BINARY_LOG" == "true" ]] && bl+=("-bl:$ARTIFACTS/log/publish-${tool}.binlog")
    # -f is required: these projects use TargetFrameworks (plural), and `publish` refuses to run without one (NETSDK1129).
    dn publish "$REPO_ROOT/${tools[$tool]}" \
      -c "$CONFIGURATION" \
      -f "$TARGET_FRAMEWORK" \
      -o "$out/$tool" \
      /p:SelfContained=false \
      --no-restore \
      -v "$(verbosity)" \
      "${BUILD_PROPS[@]}" ${bl[@]+"${bl[@]}"} \
    || error "$tool publish failed"
  done

  log "✓ Executables → $out  (invoke via 'dotnet <tool>.dll')"
}

# ── Step: pack NuGet ──────────────────────────────────────────────────────────
# Arcade normally expands $CommonMetadataElements$/$CommonFileElements$ in the .nuspec files and supplies
# NuspecBasePath, license and version. It is not imported under BUILDING_USING_DOTNET (DISABLE_ARCADE), so the
# nuspec is expanded here. Versions come from eng/Versions.props; FSHARP_PACKAGE_SUFFIX (e.g. "ce-desugar.42")
# turns every CI build into a distinct prerelease, so a feed can never silently keep a stale package.

xml_escape() { local s=$1; s=${s//&/&amp;}; s=${s//</&lt;}; s=${s//>/&gt;}; s=${s//\"/&quot;}; printf '%s' "$s"; }

step_publish_nuget() {
  log_section "Pack NuGet"
  command -v jq >/dev/null 2>&1 || error "jq is required to read the package metadata"
  shopt -u patsub_replacement 2>/dev/null || true   # bash >= 5.2: '&' in a replacement would mean "the match"
  local out="$ARTIFACTS/nupkg" gen="$ARTIFACTS/nuspec"
  mkdir -p "$out" "$gen" "$ARTIFACTS/log"

  # name | project | version property in eng/Versions.props
  local -a packages=(
    "FSharp.Core|src/FSharp.Core/FSharp.Core.fsproj|FSCorePackageVersionValue"
    "FSharp.Compiler.Service|src/Compiler/FSharp.Compiler.Service.fsproj|FSharpCompilerServicePackageVersion"
  )
  local repo_url="${GITHUB_SERVER_URL:-https://github.com}/${GITHUB_REPOSITORY:-dotnet/fsharp}"
  local authors="${PACKAGE_AUTHORS:-Microsoft, F# Software Foundation and contributors (CE-desugar fork)}"

  local entry name proj verprop
  for entry in "${packages[@]}"; do
    IFS='|' read -r name proj verprop <<<"$entry"
    log "Packing $name..."

    local -A p=()
    local k v json
    # NB: `dotnet msbuild` takes MSBuild switches (-p:Configuration=...), not `dotnet build` ones (-c ...).
    json=$(dotnet msbuild -nologo "$REPO_ROOT/$proj" "-p:Configuration=$CONFIGURATION" "${BUILD_PROPS[@]}" \
             "-getProperty:PackageId,PackageDescription,PackageTags,NuspecFile,$verprop" 2>&1) \
      || error "$name: could not evaluate the package metadata:"$'\n'"$json"
    # keep only the JSON object (first '{' .. closing '}') so stray warning lines cannot break jq
    json=$(printf '%s\n' "$json" | awk '/^\{/ {j=1} j {print} j && /^\}/ {exit}')
    [[ -n "$json" ]] || error "$name: msbuild returned no properties"
    while IFS='=' read -r k v; do p[$k]=$v; done < <(printf '%s\n' "$json" | jq -r '.Properties | to_entries[] | "\(.key)=\(.value)"')

    local version="${p[$verprop]:-}"
    [[ -n "$version" ]] || error "$name: $verprop is empty (see eng/Versions.props)"
    [[ -n "${FSHARP_PACKAGE_SUFFIX:-}" ]] && version="$version-$FSHARP_PACKAGE_SUFFIX"
    local template; template="$(dirname "$REPO_ROOT/$proj")/${p[NuspecFile]:?NuspecFile not set for $name}"
    [[ -f "$template" ]] || error "$name: nuspec template not found: $template"

    local meta
    meta="<id>$(xml_escape "${p[PackageId]:-$name}")</id>
        <version>$(xml_escape "$version")</version>
        <authors>$(xml_escape "$authors")</authors>
        <owners>$(xml_escape "$authors")</owners>
        <requireLicenseAcceptance>false</requireLicenseAcceptance>
        <description>$(xml_escape "${p[PackageDescription]:-$name}")</description>
        <license type=\"expression\">MIT</license>
        <projectUrl>$(xml_escape "$repo_url")</projectUrl>
        <tags>$(xml_escape "${p[PackageTags]:-F# fsharp functional programming}")</tags>
        <repository type=\"git\" url=\"$(xml_escape "$repo_url")\" />"

    local tpl; tpl=$(<"$template")
    tpl=${tpl//'$CommonMetadataElements$'/"$meta"}
    tpl=${tpl//'$CommonFileElements$'/}
    printf '%s\n' "$tpl" > "$gen/$name.nuspec"

    local -a bl=()
    [[ "$BINARY_LOG" == "true" ]] && bl+=("-bl:$ARTIFACTS/log/pack-${name}.binlog")
    dn pack "$REPO_ROOT/$proj" \
      -c "$CONFIGURATION" \
      -o "$out" \
      --no-restore \
      -v "$(verbosity)" \
      "/p:NuspecFile=$gen/$name.nuspec" \
      "/p:NuspecBasePath=$ARTIFACTS/bin" \
      "/p:NuspecProperties=Configuration=$CONFIGURATION" \
      "/p:PackageVersion=$version" \
      "${BUILD_PROPS[@]}" ${bl[@]+"${bl[@]}"} \
    || error "$name pack failed"
  done

  log "✓ Packages → $out"
  ls -lh "$out"/*.nupkg 2>/dev/null || log "  (none generated)"
}

# ── Step: collect release artifacts ──────────────────────────────────────────

step_collect() {
  log_section "Collect Release Artifacts"
  local rel="$ARTIFACTS/release"
  local base="$ARTIFACTS/bin"
  mkdir -p "$rel/fsc-exe" "$rel/fsi-exe"

  cp -r "$base/fsc/$CONFIGURATION/$TARGET_FRAMEWORK/." "$rel/"        2>/dev/null || true
  cp -r "$base/fsi/$CONFIGURATION/$TARGET_FRAMEWORK/." "$rel/"        2>/dev/null || true
  cp -r "$base/exe/fsc/."                              "$rel/fsc-exe/" 2>/dev/null || true
  cp -r "$base/exe/fsi/."                              "$rel/fsi-exe/" 2>/dev/null || true

  log "✓ Release → $rel"
  { tree -L 2 "$rel" 2>/dev/null || find "$rel" -type f | head -20; } >&2
}

# ── Pipelines ─────────────────────────────────────────────────────────────────

pipeline_build() {
  step_restore; step_bootstrap; step_build_compiler
  log_section "Build Complete ✓ → $ARTIFACTS/bin/"
}

pipeline_test_build() {
  step_restore; step_bootstrap; step_build_compiler; step_build_tests
  log_section "Test Build Complete ✓"
}

pipeline_test_run() {
  _validate_bootstrap; step_run_tests
}

pipeline_test_all() {
  step_restore; step_bootstrap; step_build_compiler; step_build_tests; step_run_tests
  log_section "Full Test Suite ✓"
}

pipeline_publish() {
  step_restore; step_bootstrap; step_build_compiler
  step_publish_exe; step_publish_nuget; step_collect
  log_section "Publish Complete ✓ → $ARTIFACTS"
}

# ── Main ──────────────────────────────────────────────────────────────────────

main() {
  log_section "F# Build  mode=$MODE  config=$CONFIGURATION  tfm=$TARGET_FRAMEWORK  force=$FORCE"
  log "dotnet: $(command -v dotnet)  sdk: $(dotnet --version 2>/dev/null || echo '?')  DOTNET_ROOT=${DOTNET_ROOT:-unset}"

  case "$MODE" in
    # Pipelines
    build)        pipeline_build ;;
    test-build)   pipeline_test_build ;;
    test-run)     pipeline_test_run ;;
    test-all)     pipeline_test_all ;;
    publish)      pipeline_publish ;;
    # Individual steps — surgical re-runs without running the full pipeline
    restore)      step_restore ;;
    bootstrap)    step_bootstrap ;;
    compiler)     step_build_compiler ;;
    verify)       step_verify_compiler ;;
    tests-build)  step_build_tests ;;
    tests-run)    step_run_tests ;;
    exe)          step_publish_exe ;;
    nuget)        step_publish_nuget ;;
    collect)      step_collect ;;
    # Stamp management
    clean-stamps) stamp_clear_all ;;
    clean-step)   [[ -n "$STAMP_NAME" ]] || error "clean-step requires --clean-step <name>"
                  stamp_clear "$STAMP_NAME" ;;
    *) error "Unknown mode: $MODE — run build.sh --help" ;;
  esac
}

main
