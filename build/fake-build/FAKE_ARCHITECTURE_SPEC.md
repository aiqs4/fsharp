# F# Compiler Build System - FAKE Architecture Specification

**Status**: Specification (pending approval)  
**Date**: April 15, 2026  
**Supersedes**: Old build/ FAKE files (archived)  
**Inspired by**: bash orchestration scripts (which correctly identified phases and requirements)

---

## 1. Design Philosophy

The new FAKE build system must satisfy:

1. **Modularity**: Each concern (bootstrap, compile, test, publish) is independent
2. **Type Safety**: Configuration and targets checked at compile-time
3. **Fine-Granularity**: Run individual projects, test suites, or phases
4. **Readability**: Easy for developers to understand flow and add targets
5. **Maintainability**: Changes in one module don't cascade
6. **Reproducibility**: Same inputs → same outputs always
7. **Observability**: Clear logging at each step

---

## 2. Module Architecture

### 2.1 Directory Structure

```
build/
├── build.fsx                    ← Entry point (minimal)
├── src/
│   ├── Types.fs                 ← Shared type definitions
│   ├── Config.fs                ← Configuration parsing & schema
│   ├── Logging.fs               ← Structured logging
│   ├── Paths.fs                 ← Path management
│   │
│   ├── Bootstrap/
│   │   ├── BootstrapTypes.fs    ← Types: BootstrapConfig, BootstrapResult
│   │   ├── BootstrapTargets.fs  ← FAKE targets
│   │   └── BootstrapLogic.fs    ← Core proto build logic
│   │
│   ├── Compiler/
│   │   ├── CompilerTypes.fs     ← Types: CompilerConfig, CompilerResult
│   │   ├── CompilerTargets.fs   ← FAKE targets (per-project)
│   │   └── CompilerLogic.fs     ← Core self-compile logic
│   │
│   ├── Test/
│   │   ├── TestTypes.fs         ← Types: TestSuite, TestResult
│   │   ├── TestSuites.fs        ← Define all 5 test suites
│   │   ├── TestTargets.fs       ← FAKE targets (per-suite + aggregates)
│   │   └── TestLogic.fs         ← Test build/run logic
│   │
│   ├── Publish/
│   │   ├── PublishTypes.fs      ← Types: PublishConfig, PublishResult
│   │   ├── PublishTargets.fs    ← FAKE targets
│   │   └── PublishLogic.fs      ← Exe/NuGet packaging logic
│   │
│   └── Orchestration/
│       ├── OrchestratorTypes.fs ← Types: BuildMode, OrchestratorConfig
│       ├── OrchestratorLogic.fs ← Phase coordination
│       └── OrchestratorTargets.fs ← High-level orchestration targets
│
└── README.md                    ← Build system documentation
```

### 2.2 Module Responsibilities

| Module | Responsibility | Boundary |
|--------|-----------------|----------|
| **Config** | Parse cmdline args, manage settings | Input validation, type-safe options |
| **Paths** | Directory/file management | All path constants, artifact locations |
| **Logging** | Structured logging | All I/O (console, files, logs) |
| **Bootstrap** | Proto build, file extraction | Produces: `artifacts/Bootstrap/fsc/` |
| **Compiler** | Self-compile using bootstrap | Consumes: bootstrap, produces: `artifacts/bin/*/` |
| **Test** | Build & run test suites | Consumes: compiler, produces: test results |
| **Publish** | Create exe/nupkg distributions | Consumes: compiler, produces: `artifacts/nupkg/`, `artifacts/bin/exe/` |
| **Orchestration** | Coordinate phases | Consumes: all modules, orchestrates phases |

---

## 3. Configuration Type System

### 3.1 Global Configuration

```fsharp
// build/src/Types.fs

type Configuration = 
    | Debug 
    | Release 
    | Proto

type TargetFramework = 
    | Net100   // .NET 10.0
    | Net90    // .NET 9.0
    | Net80    // .NET 8.0

type BuildMode = 
    | Build              // Phases: bootstrap → compile → verify
    | TestBuild          // Phases: test-build only
    | TestRun            // Phases: test-run only
    | TestAll            // Phases: bootstrap → compile → test-build → test-run
    | Publish            // Phases: bootstrap → compile → exe → nupkg → collect
    | PublishExe         // Phases: bootstrap → compile → exe
    | PublishNuget       // Phases: bootstrap → compile → nupkg
    | BootstrapOnly      // Phases: bootstrap only
    | CompileOnly        // Phases: bootstrap → compile

type GlobalConfig = {
    Configuration: Configuration
    TargetFramework: TargetFramework
    Verbose: bool
    BinaryLog: bool
    BuildMode: BuildMode
    // Derived paths
    Root: string
    Artifacts: string
    Bootstrap: string
}
```

### 3.2 Module Configs

Each module defines its own configuration:

```fsharp
// build/src/Bootstrap/BootstrapTypes.fs
type BootstrapConfig = {
    global: GlobalConfig
    protoProject: string
    requiredFiles: string list  // Files to validate post-build
}

type BootstrapResult = {
    success: bool
    duration: System.TimeSpan
    fscDll: string
    fsxDll: string
    fsyaccDll: string
    fslexDll: string
}

// build/src/Compiler/CompilerTypes.fs
type CompilerConfig = {
    global: GlobalConfig
    solutionPath: string
    projectsPerFramework: Map<TargetFramework, string list>
    envVarTargetsPath: string
}

type CompilerResult = {
    success: bool
    duration: System.TimeSpan
    outputPaths: Map<string, string>  // Project → DLL path
    errors: string list
}
```

---

## 4. Target Design: Fine-Granular Organization

### 4.1 Target Naming Convention

```
<Phase>-<Component>[-<Subcomponent>]
<Phase>-<Component>-*     (glob for batch operations)
```

**Examples:**

```
// Bootstrap phase
bootstrap                    → Run proto build
bootstrap:validate          → Validate bootstrap artifacts exist
bootstrap:clean             → Clean bootstrap artifacts

// Compiler phase
compiler                     → Build all compiler projects
compiler:fsc                 → Build only fsc.fsproj
compiler:fsi                 → Build only fsi.fsproj
compiler:core               → Build only FSharp.Core
compiler:service            → Build only FSharp.Compiler.Service
compiler:build              → Build only FSharp.Build
compiler:verify             → Verify compiler outputs

// Test phase (granular per-suite)
test:build                   → Build all test suites
test:build:component        → Build FSharp.Compiler.ComponentTests only
test:build:service          → Build FSharp.Compiler.Service.Tests only
test:build:scripting        → Build FSharp.Compiler.Private.Scripting.UnitTests only
test:build:msbuild          → Build FSharp.Build.UnitTests only
test:build:core             → Build FSharp.Core.UnitTests only

test:run                     → Run all pre-built tests
test:run:component          → Run only component tests
test:run:service            → Run only service tests
test:run:scripting          → Run only scripting tests
test:run:msbuild            → Run only msbuild tests
test:run:core               → Run only core tests

test:all                     → Build + run all
test:all:component          → Build + run component tests only

// Publish phase
publish                      → Full publish (exe + nupkg + artifacts)
publish:exe                  → Publish fsc/fsi executables only
publish:exe:fsc             → Publish fsc executable only
publish:exe:fsi             → Publish fsi executable only
publish:nupkg               → Pack NuGet packages only
publish:nupkg:core          → Pack FSharp.Core only
publish:nupkg:service       → Pack FSharp.Compiler.Service only
publish:collect             → Collect all artifacts to release/

// Orchestration (high-level)
build                        → DefaultTarget (bootstrap + compile + verify)
test                         → Alias for test:all
clean                        → Clean all artifacts
help                         → Show available targets
```

### 4.2 Target Dependency Graph

```
build              → bootstrap → compiler → compiler:verify
test               → bootstrap → compiler → test:build → test:run
publish            → bootstrap → compiler → publish:exe → publish:nupkg → publish:collect

test:build:*       → compiler (prereq)
test:run:*         → test:build:* (prereq)

publish:exe:*      → compiler (prereq)
publish:nupkg:*    → compiler (prereq)
publish:collect    → publish:exe + publish:nupkg (prereq)

clean              → (no prereqs, idempotent)
```

### 4.3 Target Implementation Pattern

```fsharp
// build/src/Bootstrap/BootstrapTargets.fs

Target.create "bootstrap" (fun _ ->
    let config = // ... get global config
    let result = BootstrapLogic.buildProto config
    if not result.success then failwith "Bootstrap failed"
)

// Per-suite targets (dynamically generated)
TestSuites.allSuites
|> List.iter (fun suite ->
    Target.create $"test:build:{suite.name}" (fun _ ->
        TestLogic.buildSuite suite config
    )
)

// Batch targets
Target.create "test:build" (fun _ ->
    RunTargetOrDefault "test:build:component"  // runs all test:build:* 
)
```

---

## 5. Per-Project Build Control

### 5.1 Project Registry

```fsharp
// build/src/Compiler/CompilerProjects.fs

type ProjectEntry = {
    name: string
    path: string
    frameworks: TargetFramework list
    condition: BuildMode -> bool  // Should this project build in mode X?
}

module CompilerProjects =
    let registry: ProjectEntry list = [
        { name = "fsc"
          path = "src/fsc/fscProject/fsc.fsproj"
          frameworks = [Net100; Net90; Net80]
          condition = fun _ -> true  // Build in all modes
        }
        { name = "fsi"
          path = "src/fsi/fsiProject/fsi.fsproj"
          frameworks = [Net100]
          condition = fun m -> m <> CompileOnly  // Skip in compile-only
        }
        { name = "core"
          path = "src/FSharp.Core/FSharp.Core.fsproj"
          frameworks = [Net100]
          condition = fun _ -> true
        }
        // ... more projects
    ]
    
    let forMode (mode: BuildMode): ProjectEntry list =
        registry |> List.filter (fun p -> p.condition mode)
```

### 5.2 Per-Suite Test Organization

```fsharp
// build/src/Test/TestSuites.fs

type TestSuite = {
    name: string
    projectPath: string
    displayName: string
    category: string  // "compiler" | "runtime" | "msbuild"
}

module TestSuites =
    let all: TestSuite list = [
        { name = "component"
          projectPath = "tests/FSharp.Compiler.ComponentTests/FSharp.Compiler.ComponentTests.fsproj"
          displayName = "Compiler Component Tests"
          category = "compiler"
        }
        { name = "service"
          projectPath = "tests/FSharp.Compiler.Service.Tests/FSharp.Compiler.Service.Tests.fsproj"
          displayName = "Compiler Service Tests"
          category = "compiler"
        }
        { name = "scripting"
          projectPath = "tests/FSharp.Compiler.Private.Scripting.UnitTests/FSharp.Compiler.Private.Scripting.UnitTests.fsproj"
          displayName = "FSI Scripting Tests"
          category = "runtime"
        }
        { name = "msbuild"
          projectPath = "tests/FSharp.Build.UnitTests/FSharp.Build.UnitTests.fsproj"
          displayName = "MSBuild Integration Tests"
          category = "msbuild"
        }
        { name = "core"
          projectPath = "tests/FSharp.Core.UnitTests/FSharp.Core.UnitTests.fsproj"
          displayName = "FSharp.Core Unit Tests"
          category = "runtime"
        }
    ]
    
    let byCategory cat = all |> List.filter (fun s -> s.category = cat)
```

---

## 6. Configuration File (YAML/JSON)

A `.buildconfig.yaml` in repo root for immutable settings:

```yaml
# .buildconfig.yaml
bootstrap:
  projectPath: proto.proj
  configuration: Proto
  requiredArtifacts:
    - Bootstrap/fsc/fsc.dll
    - Bootstrap/fsc/Microsoft.FSharp.Targets
    - Bootstrap/fslex/fslex.dll
    - Bootstrap/fsyacc/fsyacc.dll

compiler:
  solutionPath: FSharp.sln
  projects:
    - { name: fsc,     path: "src/fsc/fscProject/fsc.fsproj" }
    - { name: fsi,     path: "src/fsi/fsiProject/fsi.fsproj" }
    - { name: core,    path: "src/FSharp.Core/FSharp.Core.fsproj" }
    - { name: service, path: "src/Compiler/FSharp.Compiler.Service.fsproj" }
    - { name: build,   path: "src/FSharp.Build/FSharp.Build.fsproj" }

testSuites:
  - { name: component,  path: "tests/FSharp.Compiler.ComponentTests/FSharp.Compiler.ComponentTests.fsproj" }
  - { name: service,    path: "tests/FSharp.Compiler.Service.Tests/FSharp.Compiler.Service.Tests.fsproj" }
  - { name: scripting,  path: "tests/FSharp.Compiler.Private.Scripting.UnitTests/..." }
  - { name: msbuild,    path: "tests/FSharp.Build.UnitTests/FSharp.Build.UnitTests.fsproj" }
  - { name: core,       path: "tests/FSharp.Core.UnitTests/FSharp.Core.UnitTests.fsproj" }

publishing:
  nugetPackages:
    - { id: FSharp.Core,           proj: "src/FSharp.Core/FSharp.Core.fsproj" }
    - { id: FSharp.Compiler.Service, proj: "src/Compiler/FSharp.Compiler.Service.fsproj" }
```

---

## 7. Error Handling & Recovery

### 7.1 Error Classification

```fsharp
type BuildError = 
    | BootstrapFailed of string
    | CompilationFailed of project: string * errors: string list
    | TestBuildFailed of suite: string
    | TestRunFailed of suite: string * failedTests: int
    | PublishFailed of stage: string * error: string
    | ConfigError of string
    | ValidationError of string
```

### 7.2 Failure Modes

- **Soft failures**: Test failure doesn't stop subsequent test suites (collect all results)
- **Hard failures**: Compilation failure stops build immediately
- **Recoverable**: Bootstrap failure is recoverable (clean & retry)
- **Non-recoverable**: Configuration error stops immediately

---

## 8. Logging & Observability

### 8.1 Logging Levels

```fsharp
type LogLevel = 
    | Trace     // Everything (for debugging)
    | Debug     // Build details
    | Info      // Phase progress (user-relevant)
    | Warn      // Issues but continues
    | Error     // Failure state
    | Fatal     // Process terminating

type LogEntry = {
    timestamp: System.DateTime
    level: LogLevel
    phase: string  // "bootstrap", "compiler", "test", "publish"
    message: string
    context: Map<string, string>  // { project = "fsc", framework = "net10.0" }
}
```

### 8.2 Log Output

- **Console**: Human-readable, colored by level
- **Structured log**: JSON format to `artifacts/log/build.jsonl`
- **Binary log**: MSBuild binlog if `-bl` flag

---

## 9. Integration Points

### 9.1 With Nix Flake

The FAKE system will be callable from Nix flake:

```nix
devShells.default = pkgs.mkShell {
  buildInputs = [ dotnet ];
  shellHook = ''
    # Call FAKE via: dotnet fake --target build
  '';
};

packages.fsharp-compiler = runCommand "fsharp" {} ''
  # Call FAKE via: dotnet fake --target publish:collect
  mkdir -p $out
  cp -r artifacts/release/* $out/
'';
```

### 9.2 With CI/CD

GitHub Actions / Azure Pipelines will call:

```bash
dotnet fake build --target test:all
dotnet fake build --target publish
```

---

## 10. User-Facing Commands

### 10.1 CLI Interface

```bash
# Build only (default)
dotnet fake build

# Build with tests
dotnet fake build --target test:all

# Build specific test suite
dotnet fake build --target test:all:component

# Publish everything
dotnet fake build --target publish

# Publish only exe
dotnet fake build --target publish:exe

# Run help
dotnet fake build --target help

# List all targets
dotnet fake build --list-targets

# Verbose output
dotnet fake build -v

# Binary log
dotnet fake build --target publish -bl

# Clean
dotnet fake build --target clean
```

### 10.2 Options

```bash
--configuration <Debug|Release>  # Default: Release
--tfm <net10|net9|net8>          # Default: net10
-v, --verbose                    # Verbose output
-bl, --binarylog                 # Write binary log
--list-targets                   # Show all available targets
--target <name>                  # Run specific target
```

---

## 11. Success Criteria

A successful implementation achieves:

- [ ] **Modularity**: Each concern is independently testable
- [ ] **Fine-granularity**: Can build individual projects or test suites on demand
- [ ] **Type Safety**: Configuration and targets are type-checked
- [ ] **Readability**: 5 new lines of code easy to understand
- [ ] **Backward Compatibility**: All bash modes callable via FAKE targets
- [ ] **Logging**: Full audit trail in JSON format
- [ ] **Performance**: No slower than bash scripts
- [ ] **Documentation**: Clear examples for every common task

---

## 12. Migration Plan

1. **Phase 1**: Write FAKE modules in `build/src/` (no changes to root)
2. **Phase 2**: Create `build/build.fsx` entry point, test all targets work
3. **Phase 3**: Deprecate bash scripts (mark as deprecated, or keep as reference)
4. **Phase 4**: Update CI/CD to call FAKE instead of bash
5. **Phase 5**: Remove bash scripts (or archive permanently)

---

## Questions for Approval

Before implementation, please confirm:

1. ✅ Module structure correct? (or reorganize?)
2. ✅ Target naming scheme sensible? (or adjust convention?)
3. ✅ Configuration file approach sound? (or inline in code?)
4. ✅ Proceed with implementation in `build/src/`?

