#!/usr/bin/env bash
set -e

# F# Compiler Build Orchestration
# Pure shell-based orchestration without heavy MSBuild or FAKE FSI
# Used by build/ targets

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"

ARTIFACTS="$REPO_ROOT/artifacts"
BOOTSTRAP="$ARTIFACTS/Bootstrap"
PROTO_PROJ="$REPO_ROOT/proto.proj"
MAIN_SLN="$REPO_ROOT/FSharp.sln"

# Main test projects
readonly TEST_PROJECTS=(
  "tests/FSharp.Compiler.ComponentTests/FSharp.Compiler.ComponentTests.fsproj"
  "tests/FSharp.Compiler.Service.Tests/FSharp.Compiler.Service.Tests.fsproj"
  "tests/FSharp.Compiler.Private.Scripting.UnitTests/FSharp.Compiler.Private.Scripting.UnitTests.fsproj"
  "tests/FSharp.Build.UnitTests/FSharp.Build.UnitTests.fsproj"
  "tests/FSharp.Core.UnitTests/FSharp.Core.UnitTests.fsproj"
)

# Configuration defaults
CONFIGURATION="${1:-Release}"
TARGET_FRAMEWORK="${2:-net10.0}"
VERBOSE="${3:-false}"
BINARY_LOG="${4:-false}"
MODE="${5:-build}"  # build, test-build, test-run, test-all, publish

log() {
  echo "[BUILD] $*" >&2
}

log_section() {
  echo "" >&2
  echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" >&2
  echo " $*" >&2
  echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" >&2
}

error() {
  echo "[ERROR] $*" >&2
  exit 1
}

clean_bootstrap() {
  if [ -d "$BOOTSTRAP" ]; then
    log "Cleaning bootstrap artifacts..."
    rm -rf "$BOOTSTRAP"
  fi
}

build_bootstrap() {
  log_section "Building Bootstrap (Configuration=Proto)"
  
  local verbosity="minimal"
  [ "$VERBOSE" = "true" ] && verbosity="diagnostic"
  
  local args=(
    "publish" "$PROTO_PROJ"
    "-c" "Proto"
    "/p:DotNetBuild=false"
    "/p:DotNetBuildSourceOnly=false"
    "-v" "$verbosity"
  )
  
  [ "$BINARY_LOG" = "true" ] && args+=("-bl")
  
  dotnet "${args[@]}" || error "Proto build failed"
}

validate_bootstrap() {
  local required_files=(
    "$BOOTSTRAP/fsc/fsc.dll"
    "$BOOTSTRAP/fsc/fsc.runtimeconfig.json"
    "$BOOTSTRAP/fsc/Microsoft.FSharp.Targets"
    "$BOOTSTRAP/fslex/fslex.dll"
    "$BOOTSTRAP/fsyacc/fsyacc.dll"
  )
  
  for file in "${required_files[@]}"; do
    [ ! -e "$file" ] && error "Bootstrap validation failed: missing $file"
  done
  
  log "✓ Bootstrap validation passed"
}

build_compiler() {
  log_section "Building F# Compiler (Configuration=$CONFIGURATION, TFM=$TARGET_FRAMEWORK)"
  
  local verbosity="minimal"
  [ "$VERBOSE" = "true" ] && verbosity="diagnostic"
  
  local args=(
    "build" "$MAIN_SLN"
    "-c" "$CONFIGURATION"
    "-v" "$verbosity"
  )
  
  [ "$BINARY_LOG" = "true" ] && args+=("-bl")
  
  export FSHARP_TARGETS_PATH="$BOOTSTRAP/fsc/Microsoft.FSharp.Targets"
  dotnet "${args[@]}" || error "Compiler build failed"
}

verify_compiler() {
  log_section "Verifying Compiler Build"
  
  local required_files=(
    "$ARTIFACTS/bin/fsc/$CONFIGURATION/$TARGET_FRAMEWORK/fsc.dll"
    "$ARTIFACTS/bin/FSharp.Core/$CONFIGURATION/$TARGET_FRAMEWORK/FSharp.Core.dll"
  )
  
  for file in "${required_files[@]}"; do
    [ ! -e "$file" ] && error "Verification failed: missing $file"
  done
  
  log "✓ Self-compilation successful"
}

build_tests() {
  log_section "Building Tests (Configuration=$CONFIGURATION, TFM=$TARGET_FRAMEWORK)"
  
  local verbosity="minimal"
  [ "$VERBOSE" = "true" ] && verbosity="diagnostic"
  
  # Build all test projects with --no-restore (already done in main build)
  for test_proj in "${TEST_PROJECTS[@]}"; do
    local full_path="$REPO_ROOT/$test_proj"
    if [ -f "$full_path" ]; then
      log "Building $test_proj..."
      
      local args=(
        "build" "$full_path"
        "-c" "$CONFIGURATION"
        "-v" "$verbosity"
        "--no-restore"
      )
      
      [ "$BINARY_LOG" = "true" ] && args+=("-bl")
      
      export FSHARP_TARGETS_PATH="$BOOTSTRAP/fsc/Microsoft.FSharp.Targets"
      dotnet "${args[@]}" || error "Test build failed for $test_proj"
    fi
  done
  
  log "✓ Test projects built successfully"
}

run_tests() {
  log_section "Running Tests (Configuration=$CONFIGURATION, TFM=$TARGET_FRAMEWORK)"
  
  local test_count=0
  local failed_tests=()
  
  # Run all test projects with --no-build (already built)
  for test_proj in "${TEST_PROJECTS[@]}"; do
    local full_path="$REPO_ROOT/$test_proj"
    if [ -f "$full_path" ]; then
      test_count=$((test_count + 1))
      log "Running tests: $test_proj"
      
      local args=(
        "test"
        "--project" "$full_path"
        "-c" "$CONFIGURATION"
        "--no-build"
      )
      
      # Add verbosity if requested
      [ "$VERBOSE" = "true" ] && args+=("--verbosity" "detailed")
      
      export FSHARP_TARGETS_PATH="$BOOTSTRAP/fsc/Microsoft.FSharp.Targets"
      
      if ! dotnet "${args[@]}"; then
        failed_tests+=("$test_proj")
      fi
    fi
  done
  
  log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  log "Test Summary: $test_count test projects"
  
  if [ ${#failed_tests[@]} -eq 0 ]; then
    log "✓ All tests passed"
    return 0
  else
    log "✗ ${#failed_tests[@]} test project(s) failed:"
    for proj in "${failed_tests[@]}"; do
      log "  - $proj"
    done
    return 1
  fi
}

build_exe_variants() {
  log_section "Building fsc and fsi as Executables"
  
  local verbosity="minimal"
  [ "$VERBOSE" = "true" ] && verbosity="diagnostic"
  
  local exe_artifacts="$ARTIFACTS/bin/exe"
  mkdir -p "$exe_artifacts"
  
  # Note: fsc/fsi are configured as Library (DLL) for Release; this publishes them as self-contained frameworks
  # The published folder contains all dependencies and can be invoked via 'dotnet fsc.dll' or as exe wrapper
  
  # Publish fsc 
  log "Publishing fsc..."
  local fsc_args=(
    "publish" "src/fsc/fscProject/fsc.fsproj"
    "-c" "$CONFIGURATION"
    "-o" "$exe_artifacts/fsc"
    "/p:SelfContained=false"
    "-v" "$verbosity"
  )
  [ "$BINARY_LOG" = "true" ] && fsc_args+=("-bl:$ARTIFACTS/log/publish-fsc.binlog")
  export FSHARP_TARGETS_PATH="$BOOTSTRAP/fsc/Microsoft.FSharp.Targets"
  dotnet "${fsc_args[@]}" || error "fsc publish failed"
  
  # Publish fsi 
  log "Publishing fsi..."
  local fsi_args=(
    "publish" "src/fsi/fsiProject/fsi.fsproj"
    "-c" "$CONFIGURATION"
    "-o" "$exe_artifacts/fsi"
    "/p:SelfContained=false"
    "-v" "$verbosity"
  )
  [ "$BINARY_LOG" = "true" ] && fsi_args+=("-bl:$ARTIFACTS/log/publish-fsi.binlog")
  export FSHARP_TARGETS_PATH="$BOOTSTRAP/fsc/Microsoft.FSharp.Targets"
  dotnet "${fsi_args[@]}" || error "fsi publish failed"
  
  log "✓ Executables published to $exe_artifacts"
  log "fsc: $exe_artifacts/fsc (invocable via 'dotnet fsc.dll' or 'dotnet fsc')"
  log "fsi: $exe_artifacts/fsi (invocable via 'dotnet fsi.dll' or 'dotnet fsi')"
}

publish_nuget() {
  log_section "Publishing NuGet Packages"
  
  local verbosity="minimal"
  [ "$VERBOSE" = "true" ] && verbosity="diagnostic"
  
  local nuget_output="$ARTIFACTS/nupkg"
  mkdir -p "$nuget_output"
  
  # Pack FSharp.Core
  log "Packing FSharp.Core.nupkg..."
  local core_args=(
    "pack" "src/FSharp.Core/FSharp.Core.fsproj"
    "-c" "$CONFIGURATION"
    "-o" "$nuget_output"
    "-v" "$verbosity"
  )
  [ "$BINARY_LOG" = "true" ] && core_args+=("-bl:$ARTIFACTS/log/pack-core.binlog")
  export FSHARP_TARGETS_PATH="$BOOTSTRAP/fsc/Microsoft.FSharp.Targets"
  dotnet "${core_args[@]}" || error "FSharp.Core pack failed"
  
  # Pack FSharp.Compiler.Service
  log "Packing FSharp.Compiler.Service.nupkg..."
  local service_args=(
    "pack" "src/Compiler/FSharp.Compiler.Service.fsproj"
    "-c" "$CONFIGURATION"
    "-o" "$nuget_output"
    "-v" "$verbosity"
  )
  [ "$BINARY_LOG" = "true" ] && service_args+=("-bl:$ARTIFACTS/log/pack-service.binlog")
  export FSHARP_TARGETS_PATH="$BOOTSTRAP/fsc/Microsoft.FSharp.Targets"
  dotnet "${service_args[@]}" || error "FSharp.Compiler.Service pack failed"
  
  log "✓ NuGet packages published to $nuget_output"
  
  # Optional: List packages
  log "NuGet packages generated:"
  ls -lh "$nuget_output"/*.nupkg 2>/dev/null || log "  (no packages generated)"
}

collect_release_artifacts() {
  log_section "Collecting Release Artifacts"
  
  local release_dir="$ARTIFACTS/release"
  mkdir -p "$release_dir"
  
  log "Collecting compiler binaries..."
  cp -r "$ARTIFACTS/bin/fsc/$CONFIGURATION/$TARGET_FRAMEWORK/"* "$release_dir/" 2>/dev/null || true
  cp -r "$ARTIFACTS/bin/fsi/$CONFIGURATION/$TARGET_FRAMEWORK/"* "$release_dir/" 2>/dev/null || true
  cp -r "$ARTIFACTS/bin/exe/fsc/"* "$release_dir/fsc-exe/" 2>/dev/null || true
  cp -r "$ARTIFACTS/bin/exe/fsi/"* "$release_dir/fsi-exe/" 2>/dev/null || true
  
  log "✓ Release artifacts collected to $release_dir"
  
  log "Release structure:"
  tree -L 2 "$release_dir" 2>/dev/null || find "$release_dir" -type f | head -20
}

main() {
  log_section "F# Compiler Build & Test Orchestration"
  log "Configuration: $CONFIGURATION"
  log "Target Framework: $TARGET_FRAMEWORK"
  log "Mode: $MODE"
  
  case "$MODE" in
    build)
      # Bootstrap phase
      if [ ! -f "$BOOTSTRAP/fsc/fsc.dll" ]; then
        build_bootstrap
      else
        log "Using existing bootstrap"
      fi
      
      validate_bootstrap
      build_compiler
      verify_compiler
      
      log_section "Build Complete ✓"
      log "Output: $ARTIFACTS/bin/"
      ;;
    
    test-build)
      # Tests require compiler first
      if [ ! -f "$BOOTSTRAP/fsc/fsc.dll" ]; then
        build_bootstrap
      else
        log "Using existing bootstrap"
      fi
      
      validate_bootstrap
      
      if [ ! -f "$ARTIFACTS/bin/fsc/$CONFIGURATION/$TARGET_FRAMEWORK/fsc.dll" ]; then
        build_compiler
      else
        log "Using existing compiler build"
      fi
      
      build_tests
      
      log_section "Test Build Complete ✓"
      ;;
    
    test-run)
      # Assume tests already built, just run them
      log "Running pre-built tests..."
      validate_bootstrap
      export FSHARP_TARGETS_PATH="$BOOTSTRAP/fsc/Microsoft.FSharp.Targets"
      
      if run_tests; then
        log_section "Test Run Complete ✓"
        exit 0
      else
        log_section "Test Run Failed ✗"
        exit 1
      fi
      ;;
    
    test-all)
      # Full pipeline: bootstrap, build compiler, build tests, run tests
      if [ ! -f "$BOOTSTRAP/fsc/fsc.dll" ]; then
        build_bootstrap
      else
        log "Using existing bootstrap"
      fi
      
      validate_bootstrap
      build_compiler
      verify_compiler
      build_tests
      
      if run_tests; then
        log_section "Full Test Suite Complete ✓"
        exit 0
      else
        log_section "Full Test Suite Failed ✗"
        exit 1
      fi
      ;;
    
    publish)
      # Build compiler, then create executables and NuGet packages
      if [ ! -f "$BOOTSTRAP/fsc/fsc.dll" ]; then
        build_bootstrap
      else
        log "Using existing bootstrap"
      fi
      
      validate_bootstrap
      build_compiler
      verify_compiler
      
      # Create exe variants
      build_exe_variants
      
      # Pack NuGet packages
      publish_nuget
      
      # Collect all artifacts
      collect_release_artifacts
      
      log_section "Publishing Complete ✓"
      log "Artifacts location: $ARTIFACTS"
      ;;
    
    *)
      error "Unknown mode: $MODE. Supported: build, test-build, test-run, test-all, publish"
      ;;
  esac
}

main
