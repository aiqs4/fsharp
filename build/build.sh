#!/usr/bin/env bash
# build.sh — CLI wrapper for orchestrate.sh
# Each flag maps cleanly to a pipeline or individual step.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

CONFIGURATION="Release"
TARGET_FRAMEWORK="net10.0"
VERBOSE="false"
BINARY_LOG="false"
MODE="build"
FORCE="false"
JOBS="1"
STAMP_NAME=""

while [[ $# -gt 0 ]]; do
  case $1 in
    -c|--configuration)  CONFIGURATION="$2";  shift 2 ;;
    --tfm)               TARGET_FRAMEWORK="$2"; shift 2 ;;
    -v|--verbose)        VERBOSE="true";       shift ;;
    -bl|--binarylog)     BINARY_LOG="true";    shift ;;
    -f|--force)          FORCE="true";         shift ;;
    -j|--jobs)           JOBS="$2";            shift 2 ;;

    # ── Pipelines
    --test)              MODE="test-all";   shift ;;
    --test-build)        MODE="test-build"; shift ;;
    --test-run)          MODE="test-run";   shift ;;
    --publish)           MODE="publish";    shift ;;
    --exe)               MODE="exe";        shift ;;
    --nuget)             MODE="nuget";      shift ;;

    # ── Individual step re-runs
    # Available names: restore, bootstrap, compiler, verify,
    #                  tests-build, tests-run, exe, nuget, collect
    --step)              MODE="$2";         shift 2 ;;

    # ── Stamp management
    --clean-stamps)      MODE="clean-stamps";           shift ;;
    --clean-step)        MODE="clean-step"; STAMP_NAME="$2"; shift 2 ;;

    # ── Legacy FAKE target compat
    --target)
      case "$2" in
        BuildBootstrap)  MODE="build" ;;
        Test)            MODE="test-all" ;;
        *)               printf 'Unsupported --target: %s\n' "$2"; exit 1 ;;
      esac
      shift 2 ;;

    --help|-h)
      cat << 'EOF'
Usage: build.sh [options]

Options:
  -c, --configuration <cfg>  Debug|Release              (default: Release)
  --tfm <framework>          Target framework            (default: net10.0)
  -v, --verbose              Diagnostic verbosity
  -bl, --binarylog           Emit MSBuild binary log
  -f, --force                Bypass incrementalism — ignore all stamps
  -j, --jobs <n>             Parallel test build jobs    (default: 1/sequential)

Pipelines:
  (default)                  Incremental build:  restore → bootstrap → compiler
  --test                     Full pipeline + run all test projects
  --test-build               Build tests only    (compiler assumed current)
  --test-run                 Run pre-built tests (no rebuild)
  --publish                  Full publish:  exe + nuget + collect
  --exe                      Publish fsc/fsi as framework-dependent executables
  --nuget                    Pack FSharp.Core + FSharp.Compiler.Service nupkgs

Individual steps  (--step <name>):
  restore       Restore main solution (stamps; skipped if project files unchanged)
  bootstrap     Build proto bootstrap compiler
  compiler      Build F# compiler from FSharp.slnx
  verify        Assert compiler output files exist
  tests-build   Build test projects
  tests-run     Run test projects (--no-build)
  exe           Publish fsc/fsi executables
  nuget         Pack NuGet packages
  collect       Copy outputs into artifacts/release/

Stamp management:
  --clean-stamps             Wipe all stamps (next run = full rebuild)
  --clean-step <name>        Invalidate one step's stamp; force re-run of that step

Key behaviours (see orchestrate.sh for full rationale):
  - FsLexPath/FsYaccPath are pinned to Bootstrap/ via MSBuild CLI global props,
    overriding Directory.Build.targets' BUILDING_USING_DOTNET-gated assignment.
    This prevents exit 134 (SIGABRT) from Release-built tools that crash against
    the still-building in-development FSharp.Core.
  - BUILDING_USING_DOTNET=true ensures all projects route outputs to artifacts/.
  - IgnoreMibc=true skips MIBC package downloads that make restore slow.
  - SKIP_NETCURRENT_FSC_BUILD=true skips the extra netcurrent TFM that doubles
    FSharp.Compiler.Service build time in non-official local builds.

Examples:
  build.sh                          # Incremental build (skip unchanged steps)
  build.sh -f                       # Force full rebuild ignoring all stamps
  build.sh --test -j 4              # Build + test with 4 parallel test builds
  build.sh --step compiler -f -v    # Force compiler rebuild only, verbose
  build.sh --step tests-run         # Re-run tests without rebuilding
  build.sh --clean-step bootstrap   # Invalidate bootstrap stamp only
  build.sh --clean-stamps           # Reset all stamps
  build.sh -c Debug --publish       # Debug publish pipeline
  build.sh --step restore -f        # Force re-restore (e.g. after NuGet.Config change)
EOF
      exit 0 ;;

    *) printf 'Unknown option: %s\nRun "%s --help"\n' "$1" "$0"; exit 1 ;;
  esac
done

exec "$SCRIPT_DIR/orchestrate.sh" \
  "$CONFIGURATION" \
  "$TARGET_FRAMEWORK" \
  "$VERBOSE" \
  "$BINARY_LOG" \
  "$MODE" \
  "$FORCE" \
  "$JOBS" \
  "$STAMP_NAME"
