# F# Compiler FAKE Build System

Modern, modular build system for the F# Compiler project using FAKE 6.

## Quick Start

### Prerequisites
- .NET SDK 6.0 or later
- FAKE CLI: `dotnet tool install -g fake-cli`

### Basic Builds

```bash
# Full build (bootstrap + compiler + test)
dotnet fake build

# Quick compiler rebuild (assumes bootstrap valid)
dotnet fake build -t build:quick

# Release with publishing
dotnet fake build -t build:release
```

## Architecture

The build system is organized into 6 independent modules:

### 1. Bootstrap Module (`src/Bootstrap/`)
Builds the Proto compiler using the SDK, which bootstraps self-compilation.

**Files:**
- `Logic.fs` - Bootstrap build logic (validation, caching)
- `Targets.fs` - FAKE targets

**Main Targets:**
```bash
dotnet fake build -t bootstrap           # Build or use cached
dotnet fake build -t bootstrap:rebuild   # Force rebuild
dotnet fake build -t bootstrap:status    # Show status
```

### 2. Compiler Module (`src/Compiler/`)
Builds the F# compiler stack with intelligent dependency tracking via DAG.

**Files:**
- `ProjectDAG.fs` - Project discovery and dependency analysis
- `Logic.fs` - Compiler build orchestration
- `Targets.fs` - FAKE targets

**Features:**
- Automatic project discovery from .fsproj files
- Topological sort for correct build order
- Per-component building
- Parallel build support

**Main Targets:**
```bash
dotnet fake build -t compiler           # Build all (sequential)
dotnet fake build -t compiler:parallel  # Build all (parallel)
dotnet fake build -t compiler:service   # Build just Service
dotnet fake build -t compiler:core      # Build just Core
```

### 3. Test Module (`src/Test/`)
Builds and runs 6 test suites with fine-grained control.

**Files:**
- `Suites.fs` - Test suite definitions (6 suites: Component, Service, Scripting, Build, Core, Suite)
- `Logic.fs` - Test build and run logic
- `Targets.fs` - FAKE targets

**Test Suites:**
1. **ComponentTests** (40K lines) - Main IL baseline tests
2. **ServiceTests** - Language service tests
3. **ScriptingTests** - Interactive/FSI tests
4. **BuildTests** - MSBuild integration tests
5. **CoreTests** - FSharp.Core library tests
6. **SuiteTests** - Full test suite runner

**Main Targets:**
```bash
dotnet fake build -t test               # Build & run all
dotnet fake build -t test:quick         # ComponentTests only
dotnet fake build -t test:unit          # Unit tests only
dotnet fake build -t test:run:component # Run ComponentTests
```

### 4. Publish Module (`src/Publish/`)
Creates NuGet packages and publishable executables.

**Files:**
- `Logic.fs` - Pack/publish logic
- `Targets.fs` - FAKE targets

**Outputs:**
- `artifacts/publish/nuget/` - NuGet packages
- `artifacts/publish/exe/` - Executables

**Main Targets:**
```bash
dotnet fake build -t publish           # Full publish
dotnet fake build -t publish:nuget      # Pack NuGet only
dotnet fake build -t publish:exe        # Publish exe only
```

### 5. Foundation Files
Core types and configuration used by all modules.

**Files:**
- `Types.fs` - All type definitions (Configuration, BuildConfig, ProjectInfo, BuildContext)
- `Config.fs` - Configuration parsing (CLI args, environment variables)
- `Paths.fs` - Path management and project discovery

## Build Phases

The system implements 5 real build phases:

| Phase | Time | Components | Purpose |
|-------|------|-----------|---------|
| Bootstrap | 30-60s | Proto compiler | Build compiler using SDK |
| Compiler | 2-3 min | 45+ projects | Self-compile with bootstrap |
| Test Build | 1-2 min | 6 suites | Build test projects |
| Test Run | 2-5 min | 40K+ tests | Execute test suites |
| Publish | 1-2 min | nuget, exe | Create distributions |

## Configuration

### CLI Arguments
```bash
dotnet fake build -t compiler -c Release           # Release config
dotnet fake build -t compiler -p x86               # x86 platform
dotnet fake build -t compiler -tf net10.0          # net10.0 target
```

### Environment Variables
```bash
FSHARP_BUILD_CONFIG=Release         # Default configuration
FSHARP_BUILD_PARALLEL=4              # Parallelism for builds
FSHARP_BUILD_VERBOSE=true            # Verbose output
FSHARP_BUILD_STRICT=true             # Treat warnings as errors
FSHARP_BUILD_CACHE_BOOTSTRAP=true    # Cache bootstrap across runs
```

## Workflow Targets

Convenience targets for common scenarios:

```bash
dotnet fake build -t build:full      # Bootstrap → Compiler → Test
dotnet fake build -t build:quick     # Compiler only
dotnet fake build -t build:release   # Full Release build
dotnet fake build -t build:ci        # CI: Release + thorough testing
```

## Utility Targets

```bash
dotnet fake build -t status          # Show build status
dotnet fake build -t help            # Show all targets
dotnet fake build -t clean           # Clean artifacts
```

## Project Structure

```
build/
├── src/
│   ├── Types.fs                 # Type definitions
│   ├── Config.fs                # Configuration parsing
│   ├── Paths.fs                 # Path management
│   ├── Bootstrap/
│   │   ├── Logic.fs
│   │   └── Targets.fs
│   ├── Compiler/
│   │   ├── ProjectDAG.fs
│   │   ├── Logic.fs
│   │   └── Targets.fs
│   ├── Test/
│   │   ├── Suites.fs
│   │   ├── Logic.fs
│   │   └── Targets.fs
│   ├── Publish/
│   │   ├── Logic.fs
│   │   └── Targets.fs
│   └── Orchestration.fs
├── build.fsx                    # Entry point
└── paket.dependencies           # Dependencies
```

## Examples

### Build just the compiler (no tests)
```bash
dotnet fake build -t compiler
```

### Run only component tests
```bash
dotnet fake build -t test:run:component
```

### Full Release build with publishing
```bash
dotnet fake build -t build:release
```

### Parallel compiler build
```bash
dotnet fake build -t compiler:parallel
```

## Troubleshooting

### Bootstrap validation fails
```bash
dotnet fake build -t bootstrap:rebuild   # Force rebuild
```

### Tests fail
```bash
dotnet fake build -t test:build          # Rebuild tests first
dotnet fake build -t test:quick          # Run quick test only
```

### Build too slow
```bash
dotnet fake build -t compiler:parallel   # Use parallelism
```

## See Also

- [FAKE Documentation](https://fake.build)
- [F# Compiler Build Infrastructure](../DEVGUIDE.md)
- [Architecture Docs](../docs/INDEX_COMPLETE_ANALYSIS.md)
