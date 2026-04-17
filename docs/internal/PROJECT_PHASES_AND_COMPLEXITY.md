# F# Compiler Project - Phases, Components & Complexity Analysis

**Status**: Analysis document  
**Date**: April 15, 2026  
**Purpose**: Understand the real structure before building modular FAKE system

---

## Executive Summary

The F# compiler repository has **much more complexity** than the bash scripts reveal:

- **4 solution files** (not 1)
- **5 test suites** (mentioned in bash)
- **3+ build configurations** (Debug, Release, Proto)
- **2+ build architectures** (Windows/Unix differences)
- **2,500+ compiler flags** and configuration options
- **13 source modules** (not just "compiler")
- **Multiple runtime environments** (.NET Framework, netstandard, net10.0)
- **Component dependencies** form a DAG requiring careful ordering

The bash scripts simplify all this into `--test` and `--publish`, but the real build system is much deeper.

---

## Part 1: Solution Structure

### 1.1 Main Solution: FSharp.sln

Located: `/FSharp.sln` (primary, daily-use solution)

**Contents (by category)**:

#### Compiler Components (src/)
- `FSharp.Core` - Runtime library
- `FSharp.Compiler.Interactive.Settings` - FSI interactive shell settings
- `FSharp.Build` - MSBuild integration tasks
- `FSharp.DependencyManager.Nuget` - NuGet dependency resolution
- `FSharp.Compiler.Service` - Language service API (main compiler)

#### Executables (src/)
- `fsc` - F# compiler executable
- `fsi` - F# interactive shell executable

#### Test Suites (tests/)
- `FSharp.Test.Utilities` - Shared test infrastructure
- `FSharp.Compiler.ComponentTests` - Large-scale compiler testing (~40,000 lines)
- `FSharp.Compiler.Service.Tests` - Language service API tests
- `FSharp.Compiler.Private.Scripting.UnitTests` - FSI scripting tests
- `FSharp.Build.UnitTests` - MSBuild integration tests
- `FSharp.Core.UnitTests` - Runtime library tests
- `FSharpSuite.Tests` - Full end-to-end test suites

#### Benchmarks (tests/benchmarks/)
- `FSharp.Compiler.Benchmarks` - Compiler performance benchmarks
- `HistoricalBenchmark` - Historical performance tracking

#### Documentation (docs/)
- Guidelines, bootstrapping documentation, release notes

---

### 1.2 Compiler Service Solution: FSharp.Compiler.Service.sln

Located: `/FSharp.Compiler.Service.sln`

**Purpose**: IDE/tooling-focused build (smaller scope)

**Key projects**:
- `FSharp.Compiler.Service` - Only the compiler API (no exe/fsi)
- `FSharp.Compiler.Service.Tests`
- Related libraries (Core, DependencyManager)

**Use case**: 
- Language servers (Ionide, Neovim LSP)
- IDE integration tests
- Faster incremental builds for tooling

---

### 1.3 Benchmark Solution: FSharp.Benchmarks.sln

Located: `/FSharp.Benchmarks.sln`

**Contents**:
- `FSharp.Compiler.Benchmarks`
- `HistoricalBenchmark`
- `FSharp.Benchmarks.Common`

**Use case**:
- Performance regression detection
- Compiler optimization validation
- CI/CD performance gates

---

### 1.4 LSP Solution: LSPSolutionSlim.sln

Located: `/LSPSolutionSlim.sln`

**Purpose**: Language Server Protocol specific build

**Use case**:
- Fast builds for LSP server development
- IDE plugin development

---

## Part 2: Component Architecture

### 2.1 Component Dependency Graph

```
.NET SDK / Runtime
    ↓
┌─────────────────────────────────────────────────┐
│  Bootstrap Phase (Configuration=Proto)          │
│  [Uses .NET SDK's F# compiler]                  │
│  Produces: artifacts/Bootstrap/                 │
│  - fslex.dll (Lexer generator)                  │
│  - fsyacc.dll (Parser generator)                │
│  - fsc.dll (Compiler)                           │
│  - Microsoft.FSharp.Targets (MSBuild redirect)  │
└─────────────────────────────────────────────────┘
    ↓[FSharpTargetsPath redirection]
┌─────────────────────────────────────────────────┐
│  Layer 1: Foundation Libraries                  │
├─────────────────────────────────────────────────┤
│ ✓ FSharp.Core                                   │
│   - All collected types, operators, sequences  │
│   - No dependencies (self-contained)            │
│   - ~2,400 lines of F# code + 2,400 doc strings │
│   - Net Framework + netstandard2.0/2.1 support │
└─────────────────────────────────────────────────┘
    ↓[FSharp.Core]
┌─────────────────────────────────────────────────┐
│  Layer 2: Core Compiler Infrastructure          │
├─────────────────────────────────────────────────┤
│ ✓ FSharp.Compiler.Service                       │
│   - Main compiler library                       │
│   - Depends on: FSharp.Core                     │
│   - Contains: AST, Type Checking, Codegen       │
│   - Exports: public FSharpChecker API           │
│                                                  │
│ ✓ FSharp.DependencyManager.Nuget                │
│   - NuGet package resolution for #r directives │
│   - Depends on: FSharp.Core                     │
│                                                  │
│ ✓ FSharp.Compiler.Interactive.Settings         │
│   - FSI interactive session settings            │
│   - Depends on: FSharp.Core, FSharp.Compiler.Service
└─────────────────────────────────────────────────┘
    ↓[FSharp.Compiler.Service + FSharp.Core]
┌─────────────────────────────────────────────────┐
│  Layer 3: Build System & Tools                  │
├─────────────────────────────────────────────────┤
│ ✓ FSharp.Build                                  │
│   - MSBuild task implementations               │
│   - Depends on: FSharp.Core, FSharp.Compiler.Service
│   - Tasks: FSharpCompile, FSharpEmbedResXSource│
│                                                  │
│ ✓ fslex (already from bootstrap)               │
│   - Lexer generator for FSL files              │
│   - Used during fsc compilation                │
│                                                  │
│ ✓ fsyacc (already from bootstrap)              │
│   - Parser generator for FSY files             │
│   - Used during fsc compilation                │
└─────────────────────────────────────────────────┘
    ↓[All of the above]
┌─────────────────────────────────────────────────┐
│  Layer 4: Executables                           │
├─────────────────────────────────────────────────┤
│ ✓ fsc.exe                                       │
│   - F# compiler command-line tool              │
│   - Depends on: FSharp.Compiler.Service, FSharp.Build, etc.
│   - 2,500+ command-line flags (see below)      │
│                                                  │
│ ✓ fsi.exe                                       │
│   - F# interactive shell                       │
│   - Depends on: FSharp.Compiler.Service + Interactive.Settings
│   - REPL environment for scripting             │
└─────────────────────────────────────────────────┘
    ↓[fsc.exe + runtime]
┌─────────────────────────────────────────────────┐
│  Layer 5: Test Infrastructure                   │
├─────────────────────────────────────────────────┤
│ ✓ FSharp.Test.Utilities                         │
│   - Shared test helpers & infrastructure       │
│   - Depends on: FSharp.Compiler.Service        │
│                                                  │
│ ✓ Test Suites (5 total)                        │
│   1. ComponentTests (40K+ lines)               │
│   2. Service.Tests                             │
│   3. Scripting.UnitTests                       │
│   4. Build.UnitTests                           │
│   5. Core.UnitTests (net10.0 specific issues)  │
│                                                  │
│   All depend on: Test.Utilities + Compiler API │
│   Test framework: xUnit                        │
└─────────────────────────────────────────────────┘
```

---

## Part 3: Build Configurations

### 3.1 Configuration Matrix

The `.sln` file defines **6 platform x configuration combinations**:

| Configuration | Purpose | Compiler Used | Output |
|---|---|---|---|
| **Debug** | Development builds | bootstrap/fsc.dll | Unoptimized, symbols |
| **Release** | Production compiler | bootstrap/fsc.dll | Optimized |
| **Proto** | Bootstrap step | .NET SDK's fsc | Bootstrap artifacts |

| Platform | Purpose |
|---|---|
| **Any CPU** | Primary (cross-platform) |
| **x86** | Legacy Windows support |

**Total combinations from `.sln`**: 6

**But:**  
Each **test suite** can build in Debug or Release  
Each **component** can target multiple frameworks  
`fsc.fsproj` itself targets ≥3 frameworks conditionally

---

### 3.2 Target Frameworks

Not all projects target all frameworks:

| Project | net472 | netstandard2.0 | netstandard2.1 | net10.0 |
|---|---|---|---|---|
| FSharp.Core | N/A | YES | YES | YES |
| FSharp.Compiler.Service | N/A | N/A | N/A | **YES** |
| FSharp.Build | N/A | N/A | N/A | **YES** |
| fsc.exe | Windows only | N/A | N/A | **YES** |
| fsi.exe | Windows only | N/A | N/A | **YES** |
| Tests | N/A | N/A | N/A | **YES** |

**Insight**: FSharp.Core must ship multiple formats for compatibility. Compiler only ships for latest .NET.

---

## Part 4: Testing Architecture

### 4.1 Test Suite Inventory

From `FSharp.sln`:

```
FSharp.Test.Utilities
├─ xUnit test runner infrastructure
├─ Compiler invocation helpers
└─ Test result collection

FSharp.Compiler.ComponentTests
├─ Lines: ~40,000+
├─ Focus: Full compiler pipeline testing
├─ Scope: IL emission, optimization, type checking
└─ Examples: IL baselines, end-to-end scenarios

FSharp.Compiler.Service.Tests
├─ API testing for public FSharpChecker
├─ IDE integration scenarios
├─ Syntax trees, type information
└─ Editor support validation

FSharp.Compiler.Private.Scripting.UnitTests
├─ FSI (F# Interactive) testing
├─ REPL behavior, session state
├─ #load, #r directives
└─ Script execution

FSharp.Build.UnitTests
├─ MSBuild task testing
├─ Build property handling
├─ Task execution in MSBuild context
└─ Resource embedding, compilation units

FSharp.Core.UnitTests
├─ Runtime library tests
├─ Seq, List, Array, Map operations
├─ net10.0-specific: Nullable Reference Types
└─ IAsyncDisposable constraints (net10.0 new)

FSharpSuite.Tests
├─ Full end-to-end scenarios
├─ Real .fs files compiled and executed
└─ Integration test coverage
```

### 4.2 How Tests Are Run (From Bash)

The **bash scripts** invoke via `dotnet test`:

```bash
dotnet test <project.fsproj> \
  -c Release \
  --no-build \
  --project <path>
```

**But** the `.sln` defines test projects with xUnit:

```xml
<ItemGroup>
  <PackageReference Include="xunit" Version="..." />
  <PackageReference Include="xunit.runner.visualstudio" Version="..." />
</ItemGroup>
```

**Key discovery**: Tests aren't defined in `.sln` as special targets. They're just regular fsproj files with xUnit package references. The bash script treats them as executables.

---

## Part 5: Compiler Flags and Configuration

### 5.1 Option Categories

From `src/Compiler/Driver/CompilerOptions.fs` (2,479 lines):

The file defines an **OptionSpec** type system:

```fsharp
type OptionSpec =
    | OptionClear of bool ref          // --flag
    | OptionFloat of (float -> unit)   // --flag:float
    | OptionInt of (int -> unit)       // --flag:int
    | OptionSwitch of (OptionSwitch -> unit)  // --flag:(on|off)
    | OptionIntList of (int -> unit)   // --flag:int1 --flag:int2
    | OptionIntListSwitch of ...       // Complex
    | OptionRest of (string -> unit)   // Capture remaining args
    | OptionSet of bool ref            // --flag
    | OptionString of (string -> unit) // --flag:value
    | OptionStringList of ...          // --flag:value1 --flag:value2
    | OptionStringListSwitch of ...    // Complex
    | OptionUnit of (unit -> unit)     // --flag (no args)
    | OptionConsoleOnly of ...         // Console-specific
    | OptionGeneral of ...             // Custom parsing
```

### 5.2 Known Compiler Flag Categories

From `FSComp.txt` and observation:

| Category | Examples | Count ~|
|----------|----------|---------|
| **Input/Output** | `--out:file`, `--targetprofile:`, `--keyfile:` | 15+ |
| **Optimization** | `--optimize`, `--tailcalls`, `--crossoptimize` | 10+ |
| **Compilation** | `--define:`, `--warnaserror:`, `--nowarn:` | 50+ |
| **Language Features** | `--langversion:`, `--preview`, `--feature:` | 20+ |
| **Runtime** | `--targetprofile:`, `--runtimemetadataversion:` | 5+ |
| **Debugging** | `--debug:`, `--deterministic`, `--debug-emit` | 15+ |
| **Codegen** | `--staticlink`, `--pdb:`, `--embed:` | 20+ |
| **Experimental** | `--simuleException:`, `--test:` | 100+ |
| **FSI Interactive** | `--use:`, `--load:`, `--interact` | 30+ |
| **Advanced** | Platform-specific, internal testing, profiling | 1,800+? |

**Estimate**: 2,000-2,500 possible combinations of flags

---

### 5.3 Critical Flags for Build System

Most relevant to build orchestration:

```bash
fsc.exe \
  --out:output.dll \              # Output file
  --target:library                # exe|library|module|winexe
  --define:SYMBOL                 # Preprocessor defines
  --warnaserror:                  # Warnings → errors
  --optimize[+|-]                 # Optimizations
  --debug                         # Debug info
  --pdb:path                      # PDB file location
  --doc:path                      # XML docs
  --version                       # Show version
  --help                          # Show help
  --fullpaths                     # Full source paths in errors
  --tailcalls[+|-]                # Tail call optimization
  --crossoptimize[+|-]            # Cross-module optimization
```

---

## Part 6: Build Phases (Discovered)

The bootstrap docs reveal **3 distinct phases**, but closer inspection shows more:

### 6.1 Actual Build Phases

```
PHASE 0: Environment Check
├─ .NET SDK version
├─ NuGet feed access
├─ Disk space, permissions
└─ Runtime availability

PHASE 1: Bootstrap (Proto)
├─ Condition: Configuration=Proto, no FSharpTargetsPath
├─ Compiler: Uses .NET SDK's F# compiler
├─ Projects to build:
│  ├─ buildtools/fslex/fslex.fsproj
│  ├─ buildtools/fsyacc/fsyacc.fsproj
│  ├─ buildtools/AssemblyCheck/AssemblyCheck.fsproj
│  ├─ src/fsc/fscProject/fsc.fsproj (as Exe!)
│  └─ src/fsi/fsiProject/fsi.fsproj (as Exe!)
│
├─ Output: artifacts/Bootstrap/{name}/
│  ├─ fslex/fslex.dll
│  ├─ fsyacc/fsyacc.dll
│  ├─ fsc/fsc.dll                    ← NOW AN EXE!
│  ├─ fsc/Microsoft.FSharp.Targets   ← KEY FILE!
│  └─ fsc/FSharp.Build.dll
│
├─ Validation:
│  ├─ All required .dll files present?
│  ├─ fsc.runtimeconfig.json present?
│  ├─ Microsoft.FSharp.Targets loadable?
│  └─ fsc.dll executable?
│
└─ Time: ~30-60 seconds (first time only)

PHASE 2: Compiler Build (Release/Debug)
├─ Condition: Configuration != Proto
├─ Condition: Exists(ProtoOutputPath) ✓
├─ Condition: DisableCompilerRedirection != true
├─ Compiler: Now using Bootstrap/fsc.dll (via FSharpTargetsPath)
│
├─ Sequence:
│  a) FSharp.Core builds first
│     └─ Depends: Bootstrap fsc.dll only
│
│  b) FSharp.DependencyManager.Nuget
│     └─ Depends: FSharp.Core
│
│  c) FSharp.Compiler.Interactive.Settings
│     └─ Depends: FSharp.Core
│
│  d) FSharp.Build
│     └─ Depends: FSharp.Core, FSharp.Compiler.Service
│
│  e) FSharp.Compiler.Service (main compiler!)
│     └─ Depends: FSharp.Core, FSharp.DependencyManager.Nuget
│
│  f) fsc executable
│     └─ Depends: All of the above
│
│  g) fsi executable
│     └─ Depends: All of the above
│
├─ Output: artifacts/bin/{project}/Release/
│  ├─ fsc/Release/net10.0/fsc.dll
│  ├─ fsi/Release/net10.0/fsi.dll
│  ├─ FSharp.Core/Release/net13.0/FSharp.Core.dll
│  ├─ FSharp.Build/Release/net10.0/FSharp.Build.dll
│  ├─ FSharp.Compiler.Service/Release/net10.0/FSharp.Compiler.Service.dll
│  └─ All .xml documentation files
│
├─ Build Configuration Options:
│  ├─ /p:OutputType - Exe for exe, Library for dll
│  ├─ /p:PublishReadyToRun - Enable ready-to-run
│  ├─ /p:RuntimeIdentifier - For platform-specific builds
│  ├─ /p:GenerateRuntimeConfigurationFiles - Runtime config
│  └─ /p:AppendRuntimeIdentifierToOutputPath - Platform suffix
│
└─ Time: ~2-3 minutes

PHASE 3: Test Build
├─ Projects: 6 test suites (independent builds)
├─ Compiler: Uses Release fsc for compilation
├─ Configuration: Debug (tests typically debug)
└─ Output: artifacts/bin/{test-name}/Debug/net10.0/
    ├─ FSharp.Compiler.ComponentTests.dll
    ├─ FSharp.Compiler.Service.Tests.dll
    ├─ FSharp.Compiler.Private.Scripting.UnitTests.dll
    ├─ FSharp.Build.UnitTests.dll
    ├─ FSharp.Core.UnitTests.dll
    └─ FSharpSuite.Tests.dll
       └─ Time: ~1-2 minutes

PHASE 4: Test Execution
├─ Runner: xUnit via dotnet test
├─ Execution: Sequential or parallel per suite
├─ Output: JUnit XML to artifacts/test-results/
├─ Collect: Per-suite pass/fail counts
└─ Time: ~2-5 minutes (5 suites)

PHASE 5: Publishing (Optional)
├─ Sub-phase 5a: Exe Publishing
│  └─ dotnet publish {fsc,fsi}.fsproj
│     → artifacts/bin/exe/{fsc,fsi}/
│
├─ Sub-phase 5b: NuGet Packing
│  ├─ dotnet pack FSharp.Core
│  │  → artifacts/nupkg/FSharp.Core.*.nupkg
│  │
│  └─ dotnet pack FSharp.Compiler.Service
│     → artifacts/nupkg/FSharp.Compiler.Service.*.nupkg
│
└─ Sub-phase 5c: Artifact Collection
   └─ Copy all outputs to organized release/ folder
      → artifacts/release/{exe,dll,nupkg}
```

---

## Part 7: Real Project Complexity

### 7.1 Source Code Statistics

The compiler project contains:

| Component | Files | Lines (Approx) | Purpose |
|-----------|-------|---|---------|
| FSharp.Core | ~50 | 4,000 | Runtime library |
| FSharp.Compiler.Service | ~200+ | 80,000+ | Main compiler (AST, type checking, IL gen) |
| FSharp.Build | ~20 | 2,000 | MSBuild tasks |
| fsc Driver | ~5 | 3,000 | CLI interface |
| fsi Driver | ~50 | 10,000 | Interactive shell |
| Tests | ~100+ | 200,000+ | Test suites |
| Tools (fslex, fsyacc) | ~40 | 8,000 | Build generators |
| **Total** | **465+** | **~311,000** | Full compiler stack |

### 7.2 Dependency Chain Depth

```
Level 0: .NET SDK
  ↓
Level 1: Bootstrap (Proto)  [Uses SDK compiler]
  ↓
Level 2: FSharp.Core  [Compiled by bootstrap → Level 1]
  ↓
Level 3: Compiler (FCS)  [Depends on Core → Level 2]
  ↓
Level 4: MSBuild Tasks (FSharp.Build)  [Depends on FCS]
  ↓
Level 5: Executables (fsc, fsi)  [Depends on all above]
  ↓
Level 6: Tests  [Call executables or FCS API]
  ↓
Level 7: Benchmarks  [Performance tests on Level 6]
```

**Critical**: All levels must build successfully. A failure at level 3 (compiler) stops everything.

---

## Part 8: What the FAKE System Must Handle

Based on this analysis, the new FAKE build system must:

### 8.1 Modular Targets

```
bootstrap                    # Single responsibility
├─ bootstrap:tools           # fslex, fsyacc
├─ bootstrap:fsc             # fsc bootstrap exe
├─ bootstrap:fsi             # fsi bootstrap exe
├─ bootstrap:validate        # Check bootstrap success
└─ bootstrap:clean           # Clean bootstrap artifacts

compiler                      # Main compilation
├─ compiler:core             # FSharp.Core only
├─ compiler:service          # FSharp.Compiler.Service only
├─ compiler:build            # FSharp.Build only
├─ compiler:fsc              # fsc.exe only
├─ compiler:fsi              # fsi.exe only
├─ compiler:all              # All compiler projects (DAG-ordered)
└─ compiler:verify           # Validate compilation

test                          # All 6 test suites
├─ test:component            # ComponentTests only
├─ test:service              # Service.Tests only
├─ test:scripting            # Scripting.UnitTests only
├─ test:build                # Build.UnitTests only
├─ test:core                 # Core.UnitTests only
├─ test:suite                # FSharpSuite.Tests only
├─ test:all                  # All + execution
└─ test:run                  # Re-run (tests already built)

publish                       # Distribution creation
├─ publish:exe               # Create exe distributable
├─ publish:nupkg             # Pack NuGet packages
├─ publish:collect           # Organize artifacts
└─ publish:all               # All distribution types
```

### 8.2 Configuration Options

Must support:

```
--configuration <Debug|Release|Proto>
--target-framework <net8.0|net9.0|net10.0>
--optimize-level <0|1|2|3>      (tailcalls, crossopt, etc.)
--warning-level <0|1|2|3|4|5>   (nowarn, warnaserror)
--debug-info <none|embedded|full|pdbonly>
--platform <x86|Any CPU>
--parallel <n>                   (parallel job count)
--deterministic                  (reproducible builds)
--verbose / --quiet
--dry-run
--keep-temp-files
```

### 8.3 Dependency Tracking

Must understand:

```
Tests depend on:
  ├─ Compiler (all layers)
  ├─ Test.Utilities
  └─ xUnit framework

fsc/fsi depend on:
  ├─ FSharp.Compiler.Service
  ├─ FSharp.Build
  ├─ FSharp.Core
  └─ FSharp.Interactive.Settings

Compiler depends on:
  ├─ Bootstrap (redirection)
  └─ FSharp.Core + dependencies

Bootstrap depends on:
  ├─ .NET SDK
  └─ NuGet feeds
```

---

## Part 9: Revised Architecture Spec

Given this complexity, the FAKE architecture spec from earlier needs refinement:

### 9.1 Key Insights

1. **Test suites are **independent** but rely on compiler**
   - Can build in any order
   - No inter-test dependencies
   - But all need compiler first

2. **Compiler has a **strict DAG****
   - FSharp.Core → (service, build, interactive) → (fsc, fsi)
   - Cannot parallelize across layers, only within layer

3. **Bootstrap is a **gate**
   - Existence check gates redirection
   - Must succeed before real builds proceed
   - Can be cached

4. **Configuration impacts everything**
   - Proto != Release != Debug
   - Some projects conditional on configuration
   - Flags propagate down

5. **Platforms complicate builds**
   - Windows vs Unix differences
   - x86 vs Any CPU
   - Runtime identifiers for optimized builds

### 9.2 FAKE Module Structure (Revised)

```
build/src/
├── Types.fs
│   ├── BuildMode
│   ├── Configuration
│   ├── ProjectId
│   ├── TestSuiteId
│   └── Phase
│
├── Config.fs
│   └── GlobalConfig (support all options above)
│
├── Bootstrap/
│   ├── Types.fs
│   ├── Projects.fs          ← NEW: List of bootstrap projects
│   ├── Logic.fs
│   └── Targets.fs
│
├── Compiler/
│   ├── Types.fs
│   ├── ProjectDag.fs        ← NEW: Dependency DAG
│   ├── Projects.fs          ← NEW: All 7 compiler projects
│   ├── Logic.fs
│   └── Targets.fs
│
├── Test/
│   ├── Types.fs
│   ├── Suites.fs            ← NEW: All 6 test suites
│   ├── Logic.fs
│   └── Targets.fs
│
├── Publish/
│   ├── Types.fs
│   ├── Logic.fs
│   └── Targets.fs
│
└── Orchestration/
    ├── Logic.fs             ← Phase coordination
    └── Targets.fs           ← Top-level targets
```

---

## Part 10: Questions for Spec Refinement

Before implementing FAKE, answer:

1. **Parallelization Strategy**
   - Can test suites build in parallel? (Yes, independent)
   - Can compiler layers parallelize? (No, strict DAG)
   - How many parallel jobs default? (8? OS-dependent?)

2. **Configuration Combinations**
   - Support all 6 SLN combinations (Debug/Release/Proto × x86/Any CPU)?
   - Or simplify to just Debug/Release in Any CPU?
   - Keep Proto hidden or expose as target?

3. **Feature Coverage**
   - Must support 100% of compiler flags? (No, just common ones for build system)
   - Which 30-50 flags are critical for FAKE? (optimization, debugging, output)

4. **Incremental Builds**
   - Cache bootstrap across builds? (Yes)
   - Support `--no-clean` to skip rebuild? (Yes)
   - Detect stale artifacts and rebuild? (Nice to have)

5. **Build Artifacts Organization**
   - Keep current structure (`artifacts/bin/`,  `artifacts/nupkg/`)? (Yes)
   - Or reorganize? (No, changing would break CI/CD)

6. **Nix/Flake Integration**
   - FAKE targets invoked from Nix? (Yes, via `dotnet fake build --target`)
   - Or Nix calls bash scripts instead? (Bash still available as fallback)
   - Both approaches? (Yes)

---

## Document Links

- [BOOTSTRAP_GUIDE.md](BOOTSTRAP_GUIDE.md) - Full bootstrap mechanism
- [BOOTSTRAP_FILES_REFERENCE.md](BOOTSTRAP_FILES_REFERENCE.md) - File locations
- [BOOTSTRAP_QUICK_REFERENCE.md](BOOTSTRAP_QUICK_REFERENCE.md) - Quick lookup
- [FAKE_ARCHITECTURE_SPEC.md](FAKE_ARCHITECTURE_SPEC.md) - Architecture spec
