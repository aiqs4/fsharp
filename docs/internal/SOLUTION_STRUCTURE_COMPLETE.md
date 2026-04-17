# F# Compiler Solutions - Complete Mapping

## Solution File: FSharp.sln

**Primary daily-use solution**  
**File**: `/FSharp.sln`  
**Total Projects**: 45+

### Solution-Level Configurations

```
Platforms:
  - Any CPU (primary)
  - x86 (legacy Windows)

Configurations:
  - Debug|Any CPU
  - Debug|x86
  - Release|Any CPU
  - Release|x86
  - Proto|Any CPU
  - Proto|x86  ← Bootstrap config
```

---

## Project Organization by Layer

### Layer 0: Libraries (src/)

#### Core Runtime Library
```
FSharp.Core
├─ Project GUID: {DED3BBD7-53F4-428A-8C9F-27968E768605}
├─ Type: F# Library
├─ TargetFrameworks: netstandard2.0; netstandard2.1 (Release/Debug)
│                   or netstandard2.0 (Proto)
├─ Size: Medium (~4,000 lines)
├─ Dependencies: None (self-contained)
└─ Produces: FSharp.Core.dll, FSharp.Core.xml
```

#### Compiler Service (Main Compiler)
```
FSharp.Compiler.Service
├─ Project GUID: {9B4CF83C-C215-4EA0-9F8B-B5A77090F634}
├─ Type: F# Library
├─ TargetFramework: net10.0
├─ Size: Large (~80,000+ lines)
├─ Dependencies:
│  ├─ FSharp.Core
│  ├─ System.Collections.Immutable (NuGet)
│  ├─ System.Reflection.Metadata (NuGet)
│  ├─ System.Reflection.Emit (NuGet)
│  └─ System.Diagnostics.DiagnosticSource (NuGet)
└─ Produces: FSharp.Compiler.Service.dll, FSharp.Compiler.Service.xml
   With sub-modules:
    ├─ AbstractIL/                (IL generation)
    ├─ Checking/                  (Type checking)
    ├─ CodeGen/                   (Code generation)
    ├─ Driver/                    (Driver logic, options parsing)
    ├─ Interactive/               (FSI integration)
    ├─ Optimize/                  (Optimization passes)
    ├─ Service/                   (Public API)
    ├─ Symbols/                   (Symbol information)
    ├─ SyntaxTree/                (AST definition)
    ├─ TypedTree/                 (Type-checked tree)
    └─ Utilities/                 (Helpers)
```

#### Interactive Shell Settings
```
FSharp.Compiler.Interactive.Settings
├─ Project GUID: {649FA588-F02E-457C-9FCF-87E46407481E}
├─ Type: F# Library
├─ TargetFramework: net10.0
├─ Size: Small
├─ Dependencies:
│  ├─ FSharp.Core
│  └─ FSharp.Compiler.Service
└─ Purpose: FSI session configuration, defaults
```

#### Dependency Manager (NuGet Resolution)
```
FSharp.DependencyManager.Nuget
├─ Project GUID: {8B7BF62E-7D8C-4928-BE40-4E392A9EE851}
├─ Type: F# Library
├─ TargetFramework: net10.0
├─ Size: Medium
├─ Dependencies:
│  ├─ FSharp.Core
│  └─ NuGet.Protocol (NuGet)
└─ Purpose: #r "nuget: ..." resolution in FSI
```

#### MSBuild Build Tasks
```
FSharp.Build
├─ Project GUID: {702A7979-BCF9-4C41-853E-3ADFC9897890}
├─ Type: F# Library
├─ TargetFramework: net10.0
├─ Size: Medium (~2,000 lines)
├─ Dependencies:
│  ├─ FSharp.Core
│  ├─ FSharp.Compiler.Service
│  └─ System.Reflection.Emit
└─ Tasks Provided:
   ├─ FSharpCompile
   ├─ FSharpEmbedResourceText
   ├─ FSharpEmbedResXSource
   └─ FSharpSignFile
```

---

### Layer 1: Executables (src/)

#### F# Compiler Executable
```
fsc
├─ Project GUID: {10D15DBB-EFF0-428C-BA83-41600A93EEC4}
├─ Project File: src/fsc/fscProject/fsc.fsproj
├─ Type: Conditional
│       Proto:        Executable (fsc.exe)
│       Release/Debug: Library (fsc.dll)
├─ TargetFramework (proto): net10.0
├─ TargetFramework (release, Unix/dotnet): net10.0
├─ TargetFramework (release, Windows): net472; net10.0
├─ Size: Small (~1,500 lines) - thin wrapper
├─ Dependencies:
│  ├─ FSharp.Compiler.Service
│  ├─ FSharp.Build
│  └─ (All transitive)
└─ Driver: src/Compiler/Driver/fsc.fs (2,500+ compiler flags)
```

#### F# Interactive Shell Executable
```
fsi
├─ Project GUID: {68EEAB5F-8AED-42A2-BFEC-343D0AD5CB52}
├─ Project File: src/fsi/fsiProject/fsi.fsproj
├─ Type: Conditional (same as fsc)
├─ TargetFramework: same as fsc
├─ Size: Large (~10,000 lines) - full REPL
├─ Dependencies:
│  ├─ FSharp.Compiler.Service
│  ├─ FSharp.Compiler.Interactive.Settings
│  └─ (All transitive)
└─ Driver: src/Compiler/Interactive/fsi.fs
```

---

### Layer 2: Test Infrastructure (tests/)

#### Shared Test Utilities
```
FSharp.Test.Utilities
├─ Project GUID: {60D275B0-B14A-41CB-A1B2-E815A7448FCB}
├─ Type: F# Library
├─ TargetFramework: net10.0
├─ Size: Medium (~3,000 lines)
├─ Dependencies:
│  ├─ FSharp.Core
│  ├─ FSharp.Compiler.Service
│  └─ xUnit (NuGet)
└─ Provides:
   ├─ Compiler invocation helpers
   ├─ Test result collection
   └─ File system utilities
```

---

### Layer 3: Test Suites (tests/) - 6 Total

#### 1. Compiler Component Tests (Largest)
```
FSharp.Compiler.ComponentTests
├─ Project GUID: {FAC5A3BF-C0D6-437A-868A-E962AA00B418}
├─ Type: xUnit Test Project
├─ TargetFramework: net10.0
├─ Size: HUGE (~40,000+ lines)
├─ Dependencies:
│  ├─ FSharp.Test.Utilities
│  ├─ xUnit
│  └─ xUnit.Runner.VisualStudio
├─ Test Coverage:
│  ├─ Compiler frontend (parsing, type checking)
│  ├─ IL emission and verification
│  ├─ Optimization passes
│  ├─ Error diagnostics
│  ├─ Language features
│  └─ Cross-platform scenarios (~1,000s of test cases)
└─ Artifacts: IL baseline files (FSComp.txt references)
```

#### 2. Compiler Service Tests
```
FSharp.Compiler.Service.Tests
├─ Project GUID: {DDFD06DC-D7F2-417F-9177-107764EEBCD8}
├─ Type: xUnit Test Project
├─ TargetFramework: net10.0
├─ Size: Large (~5,000 lines)
├─ Dependencies:
│  ├─ FSharp.Test.Utilities
│  └─ xUnit
├─ Test Coverage:
│  ├─ Public API (FSharpChecker)
│  ├─ IDE integration
│  ├─ Incremental builds
│  ├─ Symbol information
│  └─ Type inference display
```

#### 3. FSI Scripting Tests
```
FSharp.Compiler.Private.Scripting.UnitTests
├─ Project GUID: {4FEDF286-0252-4EBC-9E75-879CCA3B85DC}
├─ Type: xUnit Test Project
├─ TargetFramework: net10.0
├─ Size: Medium (~2,000 lines)
├─ Dependencies:
│  └─ FSharp.Test.Utilities
├─ Test Coverage:
│  ├─ FSI session lifecycle
│  ├─ #load directives
│  ├─ #r "..." references
│  ├─ Interactive evaluation
│  └─ Script state management
```

#### 4. MSBuild Integration Tests
```
FSharp.Build.UnitTests
├─ Project GUID: {53C0DAAD-158C-4658-8EC7-D7341530239F}
├─ Type: xUnit Test Project
├─ TargetFramework: net10.0
├─ Size: Medium (~1,500 lines)
├─ Dependencies:
│  ├─ FSharp.Test.Utilities
│  └─ xUnit
├─ Test Coverage:
│  ├─ FSharp.Build task execution
│  ├─ Property propagation
│  ├─ Resource embedding
│  ├─ Compilation units
│  └─ Task error handling
```

#### 5. FSharp.Core Runtime Tests
```
FSharp.Core.UnitTests
├─ Project GUID: {88E2D422-6852-46E3-A740-83E391DC7973}
├─ Type: xUnit Test Project
├─ TargetFramework: net10.0
├─ Size: Large (~3,000+ lines)
├─ Dependencies:
│  ├─ FSharp.Test.Utilities
│  └─ xUnit
├─ Test Coverage:
│  ├─ Seq operations
│  ├─ List operations
│  ├─ Array operations
│  ├─ Map/Set operations
│  ├─ Async workflows
│  ├─ Computation expressions
│  └─ net10.0 specific: nullable reference types
├─ Known Issue:
│  └─ IAsyncDisposable constraints fail (type checking in net10.0)
```

#### 6. Full End-to-End Test Suite
```
FSharpSuite.Tests
├─ Project GUID: {C163E892-5BF7-4B59-AA99-B0E8079C67C4}
├─ Type: xUnit Test Project
├─ TargetFramework: net10.0
├─ Size: Large (~2,000 lines + data files)
├─ Dependencies:
│  ├─ FSharp.Test.Utilities
│  └─ xUnit
├─ Test Coverage:
│  ├─ Real .fs source files
│  ├─ Full compilation pipeline
│  ├─ Result validation
│  └─ Cross-platform end-to-end
├─ Test Data: tests/fsharp/ (hundreds of .fs files)
```

---

### Layer 4: Benchmarking Projects (tests/benchmarks/)

#### Compiler Performance Benchmarks
```
FSharp.Compiler.Benchmarks
├─ Project GUID: {9C7523BA-7AB2-4604-A5FD-653E82C2BAD1}
├─ Type: BenchmarkDotNet project
├─ TargetFramework: net10.0
├─ Benchmarks:
│  ├─ Compilation speed (various input sizes)
│  ├─ Type checking performance
│  ├─ Optimization passes
│  └─ Memory usage patterns
```

#### Historical Benchmark
```
HistoricalBenchmark
├─ Project GUID: {BEC6E796-7E53-4888-AAFC-B8FD55C425DF}
├─ Type: Benchmark comparison
├─ Purpose: Regression detection across commits
```

#### Benchmark Common Utilities
```
FSharp.Benchmarks.Common
├─ Project GUID: {7D482560-DF6F-46A5-B50C-20ECF7C38759}
├─ Type: Shared utilities
├─ Purpose: Benchmark infrastructure
```

---

### Layer 5: Test Data Projects (tests/service/data/)

#### C# Analysis Support
```
CSharp_Analysis
├─ Project GUID: {F8743670-C8D4-41B3-86BE-BBB1226C352F}
├─ Type: C# Project
├─ Purpose: Data for Compiler.Service.Tests
```

#### Test Type Provider
```
TestTP
├─ Project GUID: {7BFA159A-BF9D-4489-BF46-1B83ACCEEE0F}
├─ Type: F# Type Provider
├─ Purpose: Testing type provider support
```

---

### Solution-Level Folders (Organizational)

```
Solution Structure:
└─ FSharp.sln
   ├─ Compiler (folder)
   │  ├─ FSharp.Core
   │  ├─ FSharp.Compiler.Service
   │  ├─ FSharp.Compiler.Interactive.Settings
   │  ├─ FSharp.Build
   │  ├─ FSharp.DependencyManager.Nuget
   │  ├─ fsc
   │  └─ fsi
   │
   ├─ Tests (folder)
   │  ├─ FSharp.Test.Utilities
   │  ├─ FSharp.Compiler.ComponentTests
   │  ├─ FSharp.Compiler.Service.Tests
   │  ├─ FSharp.Compiler.Private.Scripting.UnitTests
   │  ├─ FSharp.Build.UnitTests
   │  ├─ FSharp.Core.UnitTests
   │  ├─ FSharpSuite.Tests
   │  └─ FSharp.Compiler.Service.Tests support (folder)
   │     ├─ CSharp_Analysis
   │     └─ TestTP
   │
   ├─ Libraries (folder) - Empty in this view
   │
   ├─ Tools (folder) - Empty in this view
   │
   ├─ Benchmarks (folder)
   │  ├─ FSharp.Compiler.Benchmarks
   │  ├─ HistoricalBenchmark
   │  └─ FSharp.Benchmarks.Common
   │
   ├─ Docs (folder) - Solution items only
   │
   ├─ Solution Items
   │  ├─ FSComp.txt (compiler diagnostics)
   │  └─ FSCompCheck.fsx
   │
   ├─ eng (folder) - Solution items only
   │  ├─ Version.Details.props
   │  ├─ Version.Details.xml
   │  └─ Versions.props
   │
   └─ (Documentation folders)
      ├─ docs/content/
      ├─ docs/img/
      ├─ docs/fcs/
      ├─ docs/release-notes/
      │  ├─ .FSharp.Compiler.Service/
      │  ├─ .FSharp.Core/
      │  ├─ .Language/
      │  ├─ .VisualStudio/
      │  └─ .aux/
      └─ docs/ (main docs, 400+ markdown files)
```

---

## Solution File: FSharp.Compiler.Service.sln

**IDE/Tooling-focused**  
**File**: `/FSharp.Compiler.Service.sln`

### Subset of FSharp.sln

```
Includes ONLY:
├─ FSharp.Core
├─ FSharp.Compiler.Service
├─ FSharp.Compiler.Interactive.Settings
├─ FSharp.DependencyManager.Nuget
├─ FSharp.Test.Utilities
├─ FSharp.Compiler.Service.Tests
├─ (Test data projects)

Excludes:
├─ fsc, fsi (executables)
├─ FSharp.Build
├─ All other test suites
├─ Benchmarks
└─ Full build infrastructure
```

**Use Case**: Developers working on compiler API without needing exe/fsi builds

**Build time**: ~30 seconds (much faster than FSharp.sln)

---

## Solution File: FSharp.Benchmarks.sln

**Performance-focused**  
**File**: `/FSharp.Benchmarks.sln`

### Includes

```
├─ FSharp.Benchmarks.Common
├─ FSharp.Compiler.Benchmarks
└─ HistoricalBenchmark
```

**Purpose**: Performance regression detection, tracking optimization improvements

**CI Integration**: Runs separately after main build passes

---

## Solution File: LSPSolutionSlim.sln

**Language Server Protocol**  
**File**: `/LSPSolutionSlim.sln`

### Includes

```
Minimal set for LSP server:
├─ FSharp.Core
├─ FSharp.Compiler.Service
└─ (Support projects)
```

**Purpose**: IDE (Ionide, Neovim) plugin development with fast iteration

---

## Solution Comparison Matrix

| Aspect | FSharp.sln | Service.sln | Benchmarks.sln | LSPSlim.sln |
|--------|-----------|------------|---|---|
| Projects | 45+ | ~10 | 3 | 5 |
| Includes fsc/fsi | ✓ | ✗ | ✗ | ✗ |
| Includes all tests | ✓ | ✓ (Service only) | ✗ | ✗ |
| Includes benchmarks | ✓ | ✗ | ✓ | ✗ |
| Build time | 6-12 min | 2-3 min | 3-5 min | 1-2 min |
| Use case | Full build | IDE integration | Performance | LSP plugin |
| CI | Primary | Parallel | Performance gate | Separate |

---

## Configuration Platform Mapping

### From FSharp.sln GlobalSection

```xml
GlobalSection(SolutionConfigurationPlatforms)
  Debug|Any CPU
  Debug|x86
  Proto|Any CPU
  Proto|x86
  Release|Any CPU
  Release|x86
EndGlobalSection
```

### Project-Specific Mappings

Each project maps these 6 to specific builds:

```xml
GlobalSection(ProjectConfigurationPlatforms)
{DED3BBD7...} (FSharp.Core)
  .Debug|Any CPU.Build.0
  .Debug|x86.Build.0
  .Proto|Any CPU.Build.0        → maps to Release
  .Proto|x86.Build.0            → maps to Release
  .Release|Any CPU.Build.0
  .Release|x86.Build.0
EndGlobalSection
```

**Note**: `Proto|Any CPU` typically maps to `Release|Any CPU` build for most projects, because Proto is a special configuration, not a normal build config.

---

## Key Visual: Dependency Ownership

```
By Folder:

src/
├─ FSharp.Core/              ← Runtime library (foundation)
├─ Compiler/                 ← Main compiler service
├─ FSharp.Build/             ← MSBuild integration
├─ fsc/                       ← CLI compiler
├─ fsi/                       ← Interactive shell
└─ DependencyManager/        ← NuGet resolver

tests/
├─ FSharp.Test.Utilities/    ← Shared test harness
├─ FSharp.Compiler.*/        ← Main compiler tests (6 suites)
├─ benchmarks/               ← Performance tests
└─ service/data/             ← Test data (C#, TP)

buildtools/                  ← Separate from main (lexer/parser generators)
```

All 45+ projects link back through `FSharp.Core` and `FSharp.Compiler.Service` as foundation layers.
