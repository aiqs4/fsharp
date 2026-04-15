# F# Compiler Tests - Infrastructure & Requirements

## Test Projects

The F# compiler includes these main test suites (all use xUnit):

1. **FSharp.Compiler.ComponentTests**
   - Comprehensive compiler testing
   - Tests IL emission, type checking, diagnostics
   - Required: FSharp.Compiler.Service, FSharp.Build

2. **FSharp.Compiler.Service.Tests**
   - Language Service API testing
   - IDE integration scenarios
   - Required: FSharp.Compiler.Service

3. **FSharp.Compiler.Private.Scripting.UnitTests**
   - FSI and scripting functionality
   - Required: FSharp.Compiler.Service

4. **FSharp.Build.UnitTests**
   - MSBuild integration tests
   - Required: FSharp.Build

5. **FSharp.Core.UnitTests**
   - FSharp.Core library testing
   - Required: FSharp.Core

## What's Required to Run Tests

### Build-time Requirements
- Compiler DLLs built (from orchestrate.sh build phase)
- Test project source files (.fs, .fsx)
- Test data files (.fs baseline files, IL comparisons, etc.)

### Runtime Requirements
- **dotnet CLI** - Only requirement for test execution
- **Test DLLs** - Built test assemblies
- **Compiler references** - FSharp.Compiler.Service.dll, FSharp.Core.dll, FSharp.Build.dll
- **xUnit runner** - Provided by dotnet test

## How Tests Are Isolated

Tests run **disconnected from MSBuild** - they access only:

✅ Test DLLs (in artifacts/bin/)
✅ Compiler reference assemblies (FSharp.Compiler.Service.dll, etc.)
✅ Test data files (embedded or in test project directory)
✅ FSharp.Core.dll (the runtime library)
✅ .NET runtime (via dotnet.exe)

❌ MSBuild targets are NOT loaded at test runtime
❌ Source files are NOT compiled during test run
❌ Build configuration is NOT needed

## Build Artifacts for Tests

After `orchestrate.sh` builds tests, the following are available:

```
artifacts/bin/
├── FSharp.Compiler.Service/Release/net10.0/
│   └── FSharp.Compiler.Service.dll ← Used by all tests
├── FSharp.Build/Release/net10.0/
│   └── FSharp.Build.dll
├── FSharp.Core/Release/net10.0/
│   └── FSharp.Core.dll ← Runtime library
├── FSharp.Compiler.ComponentTests/Release/net10.0/
│   └── FSharp.Compiler.ComponentTests.dll ← Test assembly
├── FSharp.Compiler.Service.Tests/Release/net10.0/
│   └── FSharp.Compiler.Service.Tests.dll ← Test assembly
├── FSharp.Compiler.Private.Scripting.UnitTests/Release/net10.0/
│   └── FSharp.Compiler.Private.Scripting.UnitTests.dll ← Test assembly
├── FSharp.Build.UnitTests/Release/net10.0/
│   └── FSharp.Build.UnitTests.dll ← Test assembly
└── FSharp.Core.UnitTests/Release/net10.0/
    └── FSharp.Core.UnitTests.dll ← Test assembly
```

## Test Execution Access Control

When `dotnet test` runs a test assembly:

1. **Loads test DLL** from bin/ directory
2. **Resolves references** using .deps.json (generated during build)
3. **Locates compiler DLLs** from artifacts/bin/
4. **Executes test methods** via xUnit
5. **Has NO access to** build files, props, targets, MSBuild

This ensures tests run in a clean, reproducible environment.

## Usage Examples

```bash
# Build everything and run tests
bash build/build.sh -c Release --test

# Build tests separately
bash build/build.sh --test-build

# Run tests only (must be pre-built)
bash build/build.sh --test-run

# Run tests with verbose output
bash build/build.sh -c Debug --verbose --test

# Run tests with binary log for diagnostics
bash build/build.sh --test -bl
```

## Minimal Test Environment

The absolute minimum needed to run a pre-built test:

```
Required on PATH:
  - dotnet (the CLI for runtime)

Required in filesystem:
  - Test DLL (e.g., FSharp.Compiler.ComponentTests.dll)
  - Dependencies (FSharp.Compiler.Service.dll, FSharp.Core.dll)
  - .deps.json (auto-generated during build, lists dependencies)
  - Test data files (embedded in DLL or relative to it)

NOT required:
  - Source code (.fs files)
  - Build system (MSBuild, FAKE, etc.)
  - Compiler frontend (fsc.exe)
  - Any props/targets files
```

Because `.deps.json` is NuGet's dependency manifest, `dotnet test` can automatically resolve all transitive dependencies without any build system knowledge.
