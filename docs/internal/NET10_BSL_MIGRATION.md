# Net10 BSL Test Migration Plan

## Root Cause

All BSL tests fail because test infrastructure hardcodes framework names, but FSharp.Core is now built with **net10.0** instead of **netstandard2.1**.

### Test Infrastructure Mismatch

**Test Code** (`tests/FSharp.Core.UnitTests/SurfaceArea.fs:22`):
```fsharp
#if NETCOREAPP
    "netstandard21"  ← Incorrect! Now should be "net10.0"
#else
    "netstandard20"
#endif
```

**What Happens**:
1. Test project compiles as **net10.0** (inherits `FSharpNetCoreProductTargetFramework=net10.0`)
2. **#if NETCOREAPP** is true (because net10 is NETCOREAPP-based)
3. Test looks for baseline: `FSharp.Core.SurfaceArea.netstandard21.{flavor}.bsl` ❌ WRONG
4. But FSharp.Core exists at: `artifacts/bin/FSharp.Core/Release/net10.0/FSharp.Core.dll`
5. Test fails: baseline file not found, assembly framework mismatch

---

## Solution Strategy

### Part 1: Create Net10 Baselines

Copy existing netstandard21 baselines to net10 versions:

```bash
# SurfaceArea baselines
cp tests/FSharp.Core.UnitTests/FSharp.Core.SurfaceArea.netstandard21.debug.bsl \
   tests/FSharp.Core.UnitTests/FSharp.Core.SurfaceArea.net10.0.debug.bsl

cp tests/FSharp.Core.UnitTests/FSharp.Core.SurfaceArea.netstandard21.release.bsl \
   tests/FSharp.Core.UnitTests/FSharp.Core.SurfaceArea.net10.0.release.bsl

cp tests/FSharp.Compiler.Service.Tests/FSharp.Compiler.Service.SurfaceArea.netstandard21.bsl \
   tests/FSharp.Compiler.Service.Tests/FSharp.Compiler.Service.SurfaceArea.net10.0.bsl

# ILVerify baselines (FSharp.Core missing net10 versions)
cp tests/ILVerify/ilverify_FSharp.Core_Debug_netstandard2.1.bsl \
   tests/ILVerify/ilverify_FSharp.Core_Debug_net10.0.bsl

cp tests/ILVerify/ilverify_FSharp.Core_Release_netstandard2.1.bsl \
   tests/ILVerify/ilverify_FSharp.Core_Release_net10.0.bsl
```

### Part 2: Update Test Infrastructure

#### 2a. SurfaceArea Tests

Update `tests/FSharp.Core.UnitTests/SurfaceArea.fs` to detect runtime context:

```fsharp
// BEFORE: Compile-time detection (wrong)
let platform =
#if NETCOREAPP
    "netstandard21"
#else
    "netstandard20"
#endif

// AFTER: Runtime detection (correct)
let assembly = typeof<int list>.Assembly
let targetFramework = 
    // Get .NET version from assembly's target framework
    let tfAttr = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()
    if tfAttr <> null then
        // Parse ".NETCoreApp,Version=v10.0" → "net10.0"
        let version = System.Text.RegularExpressions.Regex.Match(tfAttr.FrameworkName, @"Version=v([\d\.]+)").Groups.[1].Value
        $"net{version.Replace(".", "")}"
    else
        "netstandard21"

let platform = targetFramework
```

#### 2b. Compiler.fs IL Baseline Selection

Update IL baseline path selection in `tests/FSharp.Test.Utilities/Compiler.fs:241-260` to include net10:

```fsharp
let ilBslPaths = [|
    for baselineSuffix in ilBaselineSuffixes do
#if DEBUG
    #if NETCOREAPP
        yield sourceFilePath + baselineSuffix + ".il.net10.debug.bsl"    ← NEW
        yield sourceFilePath + baselineSuffix + ".il.netcore.debug.bsl"
        yield sourceFilePath + baselineSuffix + ".il.netcore.bsl"
    #else
        yield sourceFilePath + baselineSuffix + ".il.net472.debug.bsl"
        yield sourceFilePath + baselineSuffix + ".il.net472.bsl"
    #endif
#else
    #if NETCOREAPP
        yield sourceFilePath + baselineSuffix + ".il.net10.release.bsl"   ← NEW
        yield sourceFilePath + baselineSuffix + ".il.netcore.release.bsl"
        yield sourceFilePath + baselineSuffix + ".il.netcore.bsl"
    #else
        ...
#endif
|]
```

---

## Files to Create / Modify

### New Baseline Files Needed (8 total)
- `tests/FSharp.Core.UnitTests/FSharp.Core.SurfaceArea.net10.0.debug.bsl` (copy + update)
- `tests/FSharp.Core.UnitTests/FSharp.Core.SurfaceArea.net10.0.release.bsl` (copy + update)
- `tests/FSharp.Compiler.Service.Tests/FSharp.Compiler.Service.SurfaceArea.net10.0.bsl` (copy + update)
- `tests/ILVerify/ilverify_FSharp.Core_Debug_net10.0.bsl` (copy + update)
- `tests/ILVerify/ilverify_FSharp.Core_Release_net10.0.bsl` (copy + update)

### Source Code to Modify
- `tests/FSharp.Core.UnitTests/SurfaceArea.fs` — Add runtime framework detection
- `tests/FSharp.Compiler.Service.Tests/SurfaceArea.fs` — Add runtime framework detection  
- `tests/FSharp.Test.Utilities/Compiler.fs` — Add net10 IL baseline paths
- `docs/BSL_TESTS.md` — Update documentation

---

## Content Adjustments for Net10 Baselines

When copying baseline files, consider whether bracketed platform names need updating:

- `[netstandard2.1]` — may need to stay or become `[net10.0]` depending on baseline format
- `[netstandard]` — may need to become `[runtime]` or `[net10.0]`

**Recommendation**: First copy baselines verbatim, run tests with `TEST_UPDATE_BSL=1` if needed, then review diffs.

---

## Validation Steps

1. **Copy baseline files** (step 1 above)
2. **Update test code** (step 2a, 2b)
3. **Run single test locally**:
   ```bash
   dotnet test tests/FSharp.Core.UnitTests/ -c Debug -k SurfaceArea
   ```
4. **If baseline diffs appear**:
   ```bash
   TEST_UPDATE_BSL=1 dotnet test tests/FSharp.Core.UnitTests/ -c Debug
   ```
5. **Verify diffs** — ensure only framework name changes, not content
6. **Run full baseline test suite** before committing

---

## GitHub Workflow Additions (Future)

```yaml
name: BSL Tests
on: [push, pull_request]

jobs:
  surface-area-tests:
    runs-on: [ubuntu-latest]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet test tests/FSharp.Core.UnitTests/ -c Release --filter SurfaceArea
      - run: dotnet test tests/FSharp.Compiler.Service.Tests/ -c Release --filter SurfaceArea
  
  ilverify-tests:
    runs-on: [ubuntu-latest]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet test tests/ILVerify/ -c Release
```

---

## References

- **Current docs**: [BSL_TESTS.md](../BSL_TESTS.md)
- **Test infrastructure**: [tests/FSharp.Test.Utilities/Compiler.fs](../../tests/FSharp.Test.Utilities/Compiler.fs#L241)
- **Net10 target decision**: [eng/TargetFrameworks.props](../../eng/TargetFrameworks.props)
