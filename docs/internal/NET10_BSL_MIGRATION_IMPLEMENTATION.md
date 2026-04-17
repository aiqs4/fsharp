# BSL Test Net10 Migration — IMPLEMENTATION SUMMARY

## ✅ What Was Done

### 1. Created Net10 Baseline Files (5 files)
All copied from existing netstandard baselines:
```
tests/FSharp.Core.UnitTests/
  - FSharp.Core.SurfaceArea.net10.0.debug.bsl
  - FSharp.Core.SurfaceArea.net10.0.release.bsl

tests/FSharp.Compiler.Service.Tests/
  - FSharp.Compiler.Service.SurfaceArea.net10.0.bsl

tests/ILVerify/
  - ilverify_FSharp.Core_Debug_net10.0.bsl
  - ilverify_FSharp.Core_Release_net10.0.bsl
```

### 2. Updated Test Infrastructure (3 files)

#### SurfaceArea Tests: Runtime Framework Detection
**Changed** `tests/FSharp.Core.UnitTests/SurfaceArea.fs` and `tests/FSharp.Compiler.Service.Tests/SurfaceArea.fs`

**From:** Compile-time `#if NETCOREAPP` → hardcoded "netstandard21"  
**To:** Runtime reflection of assembly's TargetFrameworkAttribute → e.g., "net10.0"

**How it works:**
```fsharp
// Gets assembly target framework from metadata
let tfAttr = assembly.GetCustomAttributes(false) 
    |> Array.tryFind (fun attr -> attr.GetType().Name = "TargetFrameworkAttribute")
// Extracts version: ".NETCoreApp,Version=v10.0" → "net10.0"
```

#### IL Baselines: Added Net10 Paths
**Changed** `tests/FSharp.Test.Utilities/Compiler.fs` (lines ~243, 255)

**Added** net10 baseline paths that are checked **before** falling back to netcore:
```fsharp
#if NETCOREAPP
    yield sourceFilePath + baselineSuffix + ".il.net10.debug.bsl"    ← NEW (prioritized)
    yield sourceFilePath + baselineSuffix + ".il.netcore.debug.bsl"  ← Fallback
```

---

## ⚙️ How to Verify

### Step 1: Build the project
```bash
./build.sh -c Debug
```

### Step 2: Run Surface Area tests
```bash
dotnet test tests/FSharp.Core.UnitTests/ -c Debug --filter SurfaceAreaTest
dotnet test tests/FSharp.Compiler.Service.Tests/ -c Debug --filter SurfaceAreaTest
```

**Expected**: Tests should now PASS (finding net10 baselines)

### Step 3: Run IL Verify tests
```bash
dotnet test tests/FSharp.Compiler.ComponentTests/ -c Debug
```

**Expected**: IL baselines should find net10 versions

---

## 🔧 If Baseline Updates Are Needed

If the net10 baselines need content adjustments (API signatures differ), update and regenerate:

```bash
export TEST_UPDATE_BSL=1
dotnet test tests/FSharp.Core.UnitTests/ -c Debug --filter SurfaceAreaTest
```

Review the `.out` files for diffs, commit if correct.

---

## 📊 Test Coverage Summary

| Test Suite | Framework | Baseline | Status |
|-----------|-----------|----------|--------|
| FSharp.Core.SurfaceArea | net10.0 debug | net10.0.debug.bsl | ✓ Created |
| FSharp.Core.SurfaceArea | net10.0 release | net10.0.release.bsl | ✓ Created |
| Compiler.Service.SurfaceArea | net10.0 | net10.0.bsl | ✓ Created |
| ILVerify (FSharp.Core) | net10.0 debug | net10.0.bsl | ✓ Created |
| ILVerify (FSharp.Core) | net10.0 release | net10.0.bsl | ✓ Created |

---

## 🚀 GitHub Workflow (Optional Next Step)

A GitHub Actions workflow template is available in `docs/NET10_BSL_MIGRATION.md` to run these tests automatically on CI.

---

## 📝 Key Implementation Details

### Why Runtime Detection?
- `#if NETCOREAPP` is compile-time; all net framemworks (netstandard, net5-10, etc.) satisfy it  
- We need runtime TFM to pick the right baseline (net10.0 vs netstandard21)  
- TargetFrameworkAttribute is present in all .NET assemblies

### Baseline Fallback Chain
1. Try net10.0 baseline (preferred)
2. Try netcore baseline (backward compatibility)
3. Try generic `.il.bsl` (if exists)

This ensures old netcore baselines still work if net10 baseline missing.

---

## 📄 Related Documentation

- Full migration plan: `docs/NET10_BSL_MIGRATION.md`
- Original BSL spec: `docs/BSL_TESTS.md`
- Target frameworks: `eng/TargetFrameworks.props`
