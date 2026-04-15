#!/usr/bin/env bash
set -e

# Build wrapper - calls pure shell orchestration
# Avoids FAKE FSI environment issues while maintaining clean separation

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"

# Parse arguments
CONFIGURATION="Release"
TARGET_FRAMEWORK="net10.0"
VERBOSE="false"
BINARY_LOG="false"
MODE="build"  # build, test-build, test-run, test-all, publish, exe, nuget

while [[ $# -gt 0 ]]; do
  case $1 in
    -c|--configuration)
      CONFIGURATION="$2"
      shift 2
      ;;
    --tfm)
      TARGET_FRAMEWORK="$2"
      shift 2
      ;;
    -v|--verbose)
      VERBOSE="true"
      shift
      ;;
    -bl|--binarylog)
      BINARY_LOG="true"
      shift
      ;;
    --test)
      MODE="test-all"
      shift
      ;;
    --test-build)
      MODE="test-build"
      shift
      ;;
    --test-run)
      MODE="test-run"
      shift
      ;;
    --exe)
      MODE="publish"  # Just exe part
      shift
      ;;
    --nuget)
      MODE="publish"  # Includes nuget
      shift
      ;;
    --publish)
      MODE="publish"  # Full publish
      shift
      ;;
    --target)
      # Legacy FAKE-style target support (for compatibility)
      case "$2" in
        BuildBootstrap)
          MODE="build"
          ;;
        Test)
          MODE="test-all"
          ;;
        *)
          echo "Unsupported target: $2"
          exit 1
          ;;
      esac
      shift 2
      ;;
    --help)
      cat << EOF
Usage: $0 [options]

Options:
  -c, --configuration <config>  Debug or Release (default: Release)
  --tfm <framework>             Target framework (default: net10.0)
  -v, --verbose                 Verbose output
  -bl, --binarylog              Write MSBuild binary log
  --test                        Build and run all tests (equivalent to test-all)
  --test-build                  Build tests only (assumes compiler already built)
  --test-run                    Run tests only (assumes tests already built)
  --exe                         Build fsc and fsi as executables
  --nuget                       Create NuGet packages
  --publish                     Full publish (exe + nuget + collect artifacts)
  --target <name>               Legacy FAKE target name (BuildBootstrap, Test)
  --help                        Show this help message

Examples:
  # Build compiler only
  $0 -c Release
  
  # Build compiler in Debug and run all tests
  $0 -c Debug --test
  
  # Build tests (compiler must be pre-built)
  $0 --test-build
  
  # Run pre-built tests
  $0 --test-run
  
  # Full pipeline with verbose output
  $0 -c Release --verbose --test
  
  # Build executable variants of fsc and fsi
  $0 -c Release --exe
  
  # Create NuGet packages
  $0 -c Release --nuget
  
  # Full publish (exe + nuget + collect artifacts)
  $0 -c Release --publish
EOF
      exit 0
      ;;
    *)
      echo "Unknown option: $1"
      echo "Run '$0 --help' for usage"
      exit 1
      ;;
  esac
done

# Call orchestration script
exec "$SCRIPT_DIR/orchestrate.sh" "$CONFIGURATION" "$TARGET_FRAMEWORK" "$VERBOSE" "$BINARY_LOG" "$MODE"