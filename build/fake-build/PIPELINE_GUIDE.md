# F# Compiler Build & Test Pipeline - Complete Guide

## Overview

The new build system replaces env/build.sh with a pure shell orchestration pipeline that:
- ✅ Eliminates FAKE FSI environment complexity
- ✅ Builds F# compiler (bootstrap → self-compile)
- ✅ Builds all 5 test suites
- ✅ Runs tests with clean environment access

## Quick Start

### Build Compiler Only
```bash
cd build
bash build.sh -c Release
```

### Build and Run All Tests
```bash
bash build.sh -c Debug --test
```

### Build Tests (compiler must be pre-built)
```bash
bash build.sh --test-build
```

### Run Tests (tests must be pre-built)
```bash
bash build.sh --test-run
```

## Architecture

### Build Phases

**Phase 1: Bootstrap (Proto)**
```
dotnet publish proto.proj -c Proto
  ↓
artifacts/Bootstrap/fsc/fsc.dll
artifacts/Bootstrap/fsc/Microsoft.FSharp.Targets
artifacts/Bootstrap/fslex/fslex.dll
artifacts/Bootstrap/fsyacc/fsyacc.dll
```

**Phase 2: Self-Compile Compiler**
```
FSHARP_TARGETS_PATH=artifacts/Bootstrap/fsc/Microsoft.FSharp.Targets
dotnet build FSharp.sln -c Release
  ↓
artifacts/bin/fsc/Release/net10.0/fsc.dll
artifacts/bin/FSharp.Core/Release/net10.0/FSharp.Core.dll
artifacts/bin/FSharp.Build/Release/net10.0/FSharp.Build.dll
```

**Phase 3: Build Tests**
```
dotnet build <test-project.fsproj>
  ↓
artifacts/bin/FSharp.Compiler.ComponentTests/Debug/net10.0/FSharp.Compiler.ComponentTests.dll
artifacts/bin/FSharp.Compiler.Service.Tests/Debug/net10.0/FSharp.Compiler.Service.Tests.dll
... (5 test assemblies total)
```

**Phase 4: Run Tests**
```
dotnet test --project <test-project.fsproj> --no-build
```

## Test Requirements

### What Tests Require

Tests require ONLY these things to run successfully:

1. **Test DLL** - Built from test project
2. **Compiler References** - FSharp.Compiler.Service.dll, FSharp.Core.dll, etc.
3. **dotnet CLI** - Runtime for test execution
4. **.deps.json** - Auto-generated dependency manifest

### What Tests DON'T Access

- ❌ Source code (.fs files)
- ❌ Build system (MSBuild, props, targets)
- ❌ Compiler frontend (fsc.exe)
- ❌ Any build configuration

This ensures tests run in a clean, reproducible environment.

## Modes of Operation

### Mode: `build` (default)
```bash
bash build.sh -c Release
```
Builds compiler only (bootstrap + self-compile).

### Mode: `test-all`
```bash
bash build.sh --test
bash build.sh -c Debug --test
```
Full pipeline: bootstrap → compiler → build tests → run tests

### Mode: `test-build`
```bash
bash build.sh --test-build
```
Build tests only (assumes `dotnet build FSharp.sln` already completed).

### Mode: `test-run`
```bash
bash build.sh --test-run
```
Run tests only (assumes tests already built).

## Command-Line Options

| Option | Description | Example |
|--------|-------------|---------|
| `-c, --configuration` | Debug/Release | `bash build.sh -c Debug` |
| `--tfm` | Target framework | `bash build.sh --tfm net9.0` |
| `-v, --verbose` | Verbose output | `bash build.sh -v` |
| `-bl, --binarylog` | Write MSBuild binary log | `bash build.sh -bl` |
| `--test` | Build and run all tests | `bash build.sh --test` |
| `--test-build` | Build tests only | `bash build.sh --test-build` |
| `--test-run` | Run tests only | `bash build.sh --test-run` |
| `--target` | FAKE-style target (legacy) | `bash build.sh --target Test` |

## Test Projects Included

The pipeline builds and runs these 5 test suites:

1. **FSharp.Compiler.ComponentTests**
   - Main compiler functionality tests
   - IL emission verification
   - Type checking tests

2. **FSharp.Compiler.Service.Tests**
   - Language Service API tests
   - IDE integration scenarios

3. **FSharp.Compiler.Private.Scripting.UnitTests**
   - FSI (scripting) functionality
   - Interactive evaluation

4. **FSharp.Build.UnitTests**
   - MSBuild integration tests
   - Build system verification

5. **FSharp.Core.UnitTests**
   - FSharp.Core library tests
   - Runtime function verification

## Complete Examples

### Full Development Build and Test
```bash
cd build
bash build.sh -c Debug --test -v
```
- Builds Debug configuration compiler
- Builds all test projects
- Runs all tests with verbose output

### Quick Release Validation  
```bash
bash build.sh -c Release --test-build
bash build.sh -c Release --test-run
```
- Build tests separately (can iterate on tests without rebuilding compiler)
- Run just the tests

### CI/CD Pipeline
```bash
# Full Release build with tests
bash build.sh -c Release --test

# Optional: Generate diagnostic log
bash build.sh -c Release --test -bl
# Check artifacts/log/Build.binlog in MSBuild Viewer
```

### Rebuild Everything from Scratch
```bash
rm -rf artifacts/
bash build.sh -c Release --test
```
- Forces bootstrap rebuild
- Rebuilds compiler
- Builds and runs all tests

## Artifacts Location

After build, artifacts are in:

```
artifacts/
├── Bootstrap/                          ← Bootstrap compiler
│   ├── fsc/
│   │   ├── fsc.dll
│   │   ├── fsc.runtimeconfig.json
│   │   └── Microsoft.FSharp.Targets
│   ├── fslex/fslex.dll
│   └── fsyacc/fsyacc.dll
│
├── bin/                                ← Released/Self-compiled
│   ├── fsc/Release/net10.0/fsc.dll
│   ├── fsi/Release/net10.0/fsi.exe
│   ├── FSharp.Core/Release/net10.0/FSharp.Core.dll
│   ├── FSharp.Build/Release/net10.0/FSharp.Build.dll
│   ├── FSharp.Compiler.Service/Release/net10.0/FSharp.Compiler.Service.dll
│   ├── FSharp.Compiler.ComponentTests/Debug/net10.0/FSharp.Compiler.ComponentTests.dll
│   ├── FSharp.Compiler.Service.Tests/Debug/net10.0/FSharp.Compiler.Service.Tests.dll
│   ├── FSharp.Compiler.Private.Scripting.UnitTests/Debug/net10.0/...
│   ├── FSharp.Build.UnitTests/Debug/net10.0/...
│   └── FSharp.Core.UnitTests/Debug/net10.0/...
│
├── log/                                ← MSBuild logs (if -bl used)
│   └── Build.binlog
│
└── Temp/                               ← Test intermediate files
    └── FSharp.Test.Utilities/
        └── ...
```

## Environment Requirements

Only requirement: **dotnet CLI** installed and in PATH

```bash
dotnet --version  # Should be 10.0.201 or compatible
```

No other tools needed (no FAKE CLI, no MSBuild cleanup, no manual path setup).

## File Structure

```
build/
├── build.sh              ← Main entry point
├── orchestrate.sh        ← Core orchestration logic
├── build.fsx             ← FAKE integration (optional, for IDE)
├── Build.fs              ← FAKE module (optional, for IDE)
├── Config.fs             ← Configuration parsing
├── Paths.fs              ← Path definitions
├── Bootstrap.fs          ← Bootstrap logic
├── Compiler.fs           ← Compiler build
├── Artifacts.fs          ← Artifact collection
├── Targets.fs            ← FAKE targets
└── TEST_REQUIREMENTS.md  ← This document
```

The core logic is in `orchestrate.sh` - a pure bash script with no external dependencies.

## How It Works: Environment Isolation for Tests

When tests execute:

```
Test DLL Reference Graph
├── FSharp.Compiler.ComponentTests.dll
│   ├── Depends on: FSharp.Compiler.Service.dll
│   ├── Depends on: FSharp.Core.dll
│   ├── Depends on: xUnit testing framework
│   └── Contains: Test code + test data
│
└── Resolved via .deps.json
    ↓
    All dependencies found in artifacts/bin/
    ↓
    dotnet runtime loads them
    ↓
    Tests execute with full CLI access
```

Key insight: `.deps.json` (generated during `dotnet build`) contains all transitive dependencies. `dotnet test` reads it automatically. No build system knowledge needed.

## Error Handling

The orchestration script provides clear error messages:

```bash
[ERROR] Proto build failed with exit code 1
[ERROR] Bootstrap validation failed: missing /path/to/fsc.dll
[ERROR] Compiler build failed with exit code 1
[ERROR] Test build failed for tests/FSharp.Compiler.ComponentTests/...
[ERROR] Unknown mode: invalid-mode
```

Exit codes:
- `0` = Success
- `1` = Build/test failure (check stderr for details)

## Troubleshooting

### Build fails: "Proto build failed"
```bash
# Likely cause: Corrupted artifacts
rm -rf artifacts/
bash build.sh -c Release

# Or try with verbose output
bash build.sh -c Release -v
```

### Tests won't run: "Test projects not found"
```bash
# Ensure tests were built first
bash build.sh --test-build

# Verify DLLs exist
ls artifacts/bin/FSharp.Compiler.ComponentTests/Debug/net10.0/
```

### Some tests fail: IL verification errors
These are compiler-specific issues, not infrastructure problems. The orchestration is working correctly if:
- Tests compile ✓
- Tests execute ✓
- Test reports generated ✓

Individual test failures need compiler investigation.

## Performance

Typical build times (Release configuration, net10.0):

| Phase | Time |
|-------|------|
| Bootstrap | 30-60s |
| Compiler Build | 2-3 min |
| Test Build | 1-2 min |
| Test Execution | 2-5 min (5 suites) |
| **Total** | **~6-12 min** |

Using existing bootstrap (skipped): **~4-8 min**

## Integration with CI/CD

### GitHub Actions Example
```yaml
- name: Build F# Compiler
  run: |
    cd /repo/build
    bash build.sh -c Release --test
```

### Azure Pipelines Example
```yaml
- script: |
    cd $(Build.SourcesDirectory)/build
    bash build.sh -c Release --test
  displayName: 'Build and Test F# Compiler'
```

## Legacy Support

For scripts expecting old behavior:

```bash
# Legacy test.sh still works
bash test.sh  # Delegates to eng/build.sh --test

# FAKE targets still accessible (maps to new modes)
bash build/build.sh --target BuildBootstrap   # → bootstrap mode
bash build/build.sh --target Test              # → test-all mode
```

## Performance Optimization

**Faster iteration during development:**

```bash
# First time: full build
bash build.sh -c Debug --test

# Subsequent runs with compiler unchanged
bash build.sh -c Debug --test-build
bash build.sh -c Debug --test-run

# Debug single test by re-running just that DLL
dotnet test artifacts/bin/FSharp.Compiler.ComponentTests/Debug/net10.0/FSharp.Compiler.ComponentTests.dll --no-build
```

## Design Philosophy

The new pipeline follows these principles:

1. **No Heavy Orchestrators** - Pure shell, no FAKE FSI complexity
2. **Minimal Dependencies** - Only dotnet CLI needed
3. **Clear Phases** - Sequential, easy to follow
4. **Separate Concerns** - Bootstrap, compile, test-build, test-run are independent
5. **Reproducible** - Same commands produce same results
6. **Observable** - Clear logging at each step

## Related Documentation

- [TEST_REQUIREMENTS.md](TEST_REQUIREMENTS.md) - Test infrastructure details
- [BOOTSTRAP_GUIDE.md](../docs/BOOTSTRAP_GUIDE.md) - Bootstrap mechanism deep dive
- [BOOTSTRAP_QUICK_REFERENCE.md](../docs/BOOTSTRAP_QUICK_REFERENCE.md) - Bootstrap quick ref

## Publishing & Distribution

### Publish Mode: Create Distributables

The `--publish` mode creates all artifact types in one command:

```bash
bash build.sh -c Release --publish
```

This runs:
1. Bootstrap (if not present)
2. Compiler build
3. Executable publishing (creates runtime-dependant distributions)
4. NuGet package creation
5. Artifact collection and organization

#### Outputs

**Executable Distributions**
```bash
artifacts/bin/exe/fsc/
  ├── fsc.dll                    ← Main compiler assembly
  ├── fsc.deps.json              ← Dependency manifest
  ├── fsc.runtimeconfig.json     ← Runtime configuration  
  ├── FSharp.Core.dll            ← Bundled FSharp.Core
  ├── FSharp.Compiler.Service.dll
  ├── FSharp.Build.dll
  ├── ... (all transitive dependencies)
  └── [Callable via: dotnet fsc.dll <args>]

artifacts/bin/exe/fsi/
  └── (similar structure for F# Interactive)
```

**NuGet Packages**
```bash
artifacts/nupkg/
  ├── FSharp.Core.11.0.101-dev.nupkg          ← F# Runtime Library
  └── FSharp.Compiler.Service.43.12.101-dev.nupkg ← Compiler API
```

**Collected Release Artifacts**
```bash
artifacts/release/
  ├── (all DLLs from regular bin/fsc and bin/fsi)
  ├── fsc-exe/                    ← Executable distribution copy
  ├── fsi-exe/                    ← FSI executable distribution copy
  └── (organized for easy distribution)
```

### Create Only Executables

```bash
bash build.sh -c Release --exe
```

Publishes fsc and fsi to `artifacts/bin/exe/` with all runtime dependencies bundled.

Use case: Creating integrated tool distributions for users who want "one command" execution.

### Create Only NuGet Packages

```bash
bash build.sh -c Release --nuget
```

Creates NuGet packages in `artifacts/nupkg/`:
- `FSharp.Core.*.nupkg` - F# runtime library
- `FSharp.Compiler.Service.*.nupkg` - Compiler API library

#### Uploading to NuGet.org

```bash
# After creating packages with --nuget or --publish
dotnet nuget push artifacts/nupkg/*.nupkg \
  --api-key $NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

#### Uploading to Private Feed

```bash
dotnet nuget push artifacts/nupkg/*.nupkg \
  --api-key $INTERNAL_FEED_KEY \
  --source https://mycompany.com/nuget/feed
```

### Viewing Package Contents

```bash
# Extract and inspect NuGet package
unzip -l artifacts/nupkg/FSharp.Core.*.nupkg | head -20

# Check package metadata
dotnet nuget locals global-packages --clear  # Optional: clear cache
dotnet package search FSharp.Core  # Verify in feed
```

## Complete Release Workflow

### 1. Build and Validate

```bash
# Full debug build with tests
bash build.sh -c Debug --test

# If all tests pass, proceed to release build
bash build.sh -c Release --test
```

### 2. Create Distributables

```bash
# Create everything: exe + nupkg + organized artifacts
bash build.sh -c Release --publish

# Review generated artifacts
ls -lh artifacts/release/
ls -lh artifacts/nupkg/
ls -lh artifacts/bin/exe/
```

### 3. Verify Artifacts

```bash
# Test fsc executable
cd artifacts/bin/exe/fsc
dotnet fsc.dll --help

# Inspect nupkg contents
unzip -l ../../nupkg/FSharp.Core*.nupkg | grep -E "lib/|\.nuspec"
```

### 4. Create Release Notes

```bash
# List changes since last release
git log --oneline <previous-release>..HEAD

# Document in release-notes.md
```

### 5. Push to NuGet

```bash
# Push packages
dotnet nuget push artifacts/nupkg/FSharp*.nupkg \
  --api-key $NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

### 6. Tag Release

```bash
git tag -a v<version> -m "Release <version>"
git push origin v<version>
```

## Publishing Configuration

### Nuspec Files

Package metadata is defined in `.nuspec` files:

- `src/FSharp.Core/FSharp.Core.nuspec` - FSharp.Core package definition
- `src/Compiler/FSharp.Compiler.Service.nuspec` - Compiler Service package definition

These control:
- Package dependencies
- Target frameworks
- Included files
- License information

### Version Management

Versions are governed by:
- `src/FSharp.Core/FSharp.Core.fsproj` - `PackageVersionPrefix="$(FSCorePackageVersion)"`
- `src/Compiler/FSharp.Compiler.Service.fsproj` - `PackageVersionPrefix="$(FSCompilerPackageVersion)"`

Set via MSBuild properties or environment variables.

## Exe vs DLL: Understanding Outputs

### Why DLL-based Executables?

The published distributions contain:
- `fsc.dll` - Main assembly (must be DLL for MSBuild integration)
- `fsc.runtimeconfig.json` - Runtime configuration
- All dependencies bundled

They're callable via `dotnet fsc.dll` - effectively an "exe" but portable.

### Why Not Single-File Exe?

The projects are built as Libraries (DLL) to support:
- Integration with MSBuild as tasks (requires DLL refs)
- Reuse in IDEs and tooling
- Cross-platform packaging

For true `.exe` files on Windows, would require:
- Separate project configuration with `OutputType=Exe`
- `/p:PublishSingleFile=true` in publish command
- Trade-off: Single file but less flexible

Current model balances: **Flexibility + Distribution + Tooling**.

---

**Last Updated**: April 15, 2026  
**Status**: Publishing complete and tested
