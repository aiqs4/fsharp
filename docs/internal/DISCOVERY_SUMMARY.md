# What I Found: F# Compiler Project Complexity

## Quick Answer to Your Questions

### "Where can I build unit-tests or run component tests?"

They're **not special targets** in the `.sln`. They're just regular F# projects with xUnit package references:

```
tests/
├─ FSharp.Compiler.ComponentTests/FSharp.Compiler.ComponentTests.fsproj
├─ FSharp.Compiler.Service.Tests/FSharp.Compiler.Service.Tests.fsproj
├─ FSharp.Compiler.Private.Scripting.UnitTests/...
├─ FSharp.Build.UnitTests/...
├─ FSharp.Core.UnitTests/...
└─ FSharpSuite.Tests/...
```

The bash script finds them and runs `dotnet test` on each one. That's it. They build via standard MSBuild, then xUnit discovers and runs the tests.

---

### "How many parts and phases does this project have?"

**Much more than the bash script reveals:**

**4 Solution Files:**
- `FSharp.sln` - Main (100+ projects)
- `FSharp.Compiler.Service.sln` - IDE tooling only
- `FSharp.Benchmarks.sln` - Performance tests
- `LSPSolutionSlim.sln` - Language server

**7 Layers of Dependencies:**
```
.NET SDK
  ↓ [uses SDK compiler]
Bootstrap (Proto phase)
  ↓ [builds]
FSharp.Core [Layer 1 - runtime library]
  ↓ 
Compiler Libraries [Layer 2 - AST, type checking, codegen]
  ├─ FSharp.Compiler.Service (main compiler)
  ├─ FSharp.DependencyManager.Nuget
  └─ FSharp.Compiler.Interactive.Settings
  ↓
Build System [Layer 3 - MSBuild tasks]
  └─ FSharp.Build
  ↓
Executables [Layer 4]
  ├─ fsc.exe (compiler CLI)
  └─ fsi.exe (interactive shell)
  ↓
Tests [Layer 5 - 6 test suites]
  ├─ ComponentTests (~40,000 lines)
  ├─ Service.Tests
  ├─ Scripting.UnitTests
  ├─ Build.UnitTests
  ├─ Core.UnitTests
  └─ FSharpSuite.Tests
  ↓
Benchmarks [Layer 6]
```

**5 Build Phases** (bash hides most of this):
1. **Bootstrap** - Build proto compiler using SDK (30-60 sec)
2. **Compiler** - Self-compile using bootstrap (2-3 min)
3. **Tests Build** - Compile all test projects (1-2 min)
4. **Tests Run** - Execute xUnit tests (2-5 min)
5. **Publish** - Create exe + nupkg distributions (1-2 min)

**6 Build Configurations:** (from `.sln` combos)
- Debug × (Any CPU + x86)
- Release × (Any CPU + x86)
- Proto × (Any CPU + x86)

**4 Target Frameworks:** (projects target different ones)
- net472 (Windows legacy)
- netstandard2.0 (compatibility)
- netstandard2.1 (compatibility)
- net10.0 (primary)

---

## Compiler Flags

### How Many?

**~2,500 possible combinations** of compiler flags. Found in `src/Compiler/Driver/CompilerOptions.fs` (2,479 lines).

### Most Important Flags for Build System

```bash
# Output control
--out:file                  # Output file
--target:library|exe|module # Output type

# Compilation
--define:SYMBOL             # Preprocessor
--warnaserror:              # Treat warnings as errors
--optimize[+|-]             # Enable optimizations
--debug                     # Debug info

# Performance
--tailcalls[+|-]            # Tail call optimization
--crossoptimize[+|-]        # Cross-module optimization

# Reproducibility
--deterministic             # Reproducible behavior
--pdb:path                  # PDB location

# Plus hundreds of internal/experimental flags...
```

---

## The Real Structure You Need for FAKE

### What the Bash Scripts Hide

Your bash scripts simplify everything into:
- `MODE=build` → "$CONFIGURATION"
- `MODE=test-all` → bootstrap + compile + test-build + test-run
- `MODE=publish` → exe + nupkg

**But the real system has:**

1. **3 Independent Phase Types** (not 1 script)
   - Bootstrap (Proto config only, special conditions)
   - Compiler (Release/Debug config, layer DAG)
   - Test (builds 6 suites independently, then runs each)

2. **7-Project Dependency DAG** (not monolithic)
   ```
   Core → Service → Build → Exe
              ↓
         Scripting Settings
   ```

3. **6 Configuration Combinations** (not just "Release")
   - Each has different paths, outputs
   - Some projects conditional on config

4. **Per-Project Control** (not just "build all")
   - Build only FSharp.Core? Possible
   - Build only Service? Possible
   - Build only tests? Possible

---

## What the NEW FAKE System Should Expose

### Fine-Grained Targets

```bash
# Bootstrap phase (hidden in bash, exposed in FAKE)
dotnet fake build --target bootstrap             # All bootstrap projects
dotnet fake build --target bootstrap:fsc         # Just fsc bootstrap
dotnet fake build --target bootstrap:validate    # Verify bootstrap artifacts

# Compiler phase (hidden in bash, exposed in FAKE)
dotnet fake build --target compiler              # All projects (DAG-ordered)
dotnet fake build --target compiler:core         # Just FSharp.Core
dotnet fake build --target compiler:service      # Just FSharp.Compiler.Service
dotnet fake build --target compiler:fsc          # Just fsc.exe
dotnet fake build --target compiler:fsi          # Just fsi.exe

# Test phase (hidden in bash, exposed in FAKE)
dotnet fake build --target test:build            # Build all test suites
dotnet fake build --target test:run              # Run all tests (pre-built)
dotnet fake build --target test:all              # Build + run

# Per-suite fine-granularity
dotnet fake build --target test:component        # Only ComponentTests
dotnet fake build --target test:service          # Only Service.Tests
dotnet fake build --target test:build:core       # Build + run Core.UnitTests only

# Publishing
dotnet fake build --target publish               # All (exe + nupkg)
dotnet fake build --target publish:exe           # Just executables
dotnet fake build --target publish:nupkg         # Just NuGet packages
```

### Configuration Options

```bash
dotnet fake build \
  --target build \
  --configuration Release \
  --target-framework net10.0 \
  --parallel 8 \
  --optimize \
  --verbose
```

---

## Documents I Created for You

1. **[FAKE_ARCHITECTURE_SPEC.md](FAKE_ARCHITECTURE_SPEC.md)**
   - Modular structure: `build/src/{Bootstrap,Compiler,Test,Publish,Orchestration}/`
   - Fine-grained targets: `bootstrap`, `compiler:core`, `test:component`, etc.
   - Configuration schema: type-safe option parsing
   - Dependency DAG handling
   - 10 sections explaining design

2. **[PROJECT_PHASES_AND_COMPLEXITY.md](PROJECT_PHASES_AND_COMPLEXITY.md)** ← NEW, comprehensive
   - All 4 solution files mapped
   - Complete dependency layers
   - Test suite inventory (6 suites detailed)
   - Compiler flag categories (2,500+)
   - 5 real build phases with timing
   - 7-project dependency DAG
   - Comparison: bash simplification vs. real structure

3. **[Bootstrap docs](.) (existing)**
   - BOOTSTRAP_GUIDE.md - Mechanism explanation
   - BOOTSTRAP_FILES_REFERENCE.md - File locations
   - BOOTSTRAP_QUICK_REFERENCE.md - Cheat sheet
   - BOOTSTRAP_INSIGHTS.md - net10.0 lessons

---

## Your Next Steps

### Option A: Review & Approve Spec
1. Read `FAKE_ARCHITECTURE_SPEC.md` (design)
2. Read `PROJECT_PHASES_AND_COMPLEXITY.md` (reality)
3. Tell me:
   - Module structure: ✓ or adjust?
   - Target naming: ✓ or adjust?
   - Configuration options: ✓ or add/remove?
   - Anything else before I code?

### Option B: Start Building
If you're happy with the spec, I'll implement phase by phase:
1. `build/src/Types.fs` - Type definitions
2. `build/src/Config.fs` - Configuration parsing
3. `build/src/Bootstrap/` - Bootstrap module
4. `build/src/Compiler/` - Compiler module with DAG
5. `build/src/Test/` - Test suites module
6. `build/src/Publish/` - Publishing module
7. `build/src/Orchestration/` - Top-level coordination

Each module would have ~100-300 lines, well-structured, independently testable.

### Option C: Refine Spec Further
Questions from my analysis (see Part 10 of complexity doc):

1. Parallelization strategy?
2. Support all 6 configuration combinations or simplify?
3. Which 30-50 compiler flags are critical? (Not all 2,500)
4. Cache bootstrap across invocations?
5. Keep `artifacts/bin/` structure or reorganize?

---

## Key Insight

The **bash scripts are good** at hiding complexity, but they also hide **architectural decisions** that FAKE needs to make explicit:

- **When does bootstrap cache?** (bash checks file existence)
- **How does compiler DAG build?** (bash just runs `dotnet build FSharp.sln`)
- **Can tests parallelize?** (bash doesn't, but tests are independent)
- **What triggers re-bootstrap?** (bash never rebuilds if exists)

FAKE forces these decisions into **type system** and **configuration**, which is good.

---

## Ready to Build?

Tell me:

1. **Spec approval**: Parts to adjust?
2. **Configuration**: What options matter most?
3. **Feature scope**: 100% compatibility with bash, or can we drop anything?
4. **Timeline**: Build all at once or module-by-module (want feedback as we go)?

Then I'll implement it as **sound, modular, type-safe FAKE build system** that Nix can call cleanly.
