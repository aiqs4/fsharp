# BSL Net10 Migration — Quick Start Checklist

## What Was Done ✅

- [x] Created 5 net10 baseline files (from netstandard copies)
- [x] Updated SurfaceArea tests for runtime TFM detection
- [x] Updated IL baseline path selection to prioritize net10
- [x] Added documentation of changes

## What You Should Do Next 🎯

### Immediate (Verify it Works)
```bash
# 1. Build
./build.sh -c Debug

# 2. Run surface area tests
nix-shell --run "dotnet test tests/FSharp.Core.UnitTests/ -c Debug --filter SurfaceAreaTest"
nix-shell --run "dotnet test tests/FSharp.Compiler.Service.Tests/ -c Debug --filter SurfaceAreaTest"

# 3. Check if they pass (they should!)
```

### If Tests Fail
Common issues and fixes:

| Symptom | Fix |
|---------|-----|
| "Baseline not found" | Verify net10.0 files were created in correct directories |
| API signature diffs | Run with `TEST_UPDATE_BSL=1` to auto-update baselines (review changes first) |
| Wrong framework detected | Check assembly's TargetFrameworkAttribute is set correctly |

### Before Committing
```bash
# Run full test suite
nix-shell --run "./build.sh -c Release --testcoreclr"

# Check git status
git status docs/BSL_TESTS.md  # May need updating if approach changed
git diff tests/FSharp.Core.UnitTests/SurfaceArea.fs  # Verify changes
git diff tests/FSharp.Compiler.Service.Tests/SurfaceArea.fs
git diff tests/FSharp.Test.Utilities/Compiler.fs
```

### For CI/GitHub (Optional, Future)
Template workflow available in: `docs/NET10_BSL_MIGRATION.md`

---

## Files Modified/Created

### New Files (Baseline Copies)
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

### Modified Source Files
```
tests/FSharp.Core.UnitTests/SurfaceArea.fs
  - Replaced #if NETCOREAPP detection with runtime reflection
  
tests/FSharp.Compiler.Service.Tests/SurfaceArea.fs
  - Replaced hardcoded "netstandard21" with runtime TFM detection
  
tests/FSharp.Test.Utilities/Compiler.fs (lines ~243, 255)
  - Added net10 IL baseline paths (before netcore fallback)
```

### Documentation
```
docs/NET10_BSL_MIGRATION.md
  - Full implementation plan with code examples
  
docs/NET10_BSL_MIGRATION_IMPLEMENTATION.md
  - Step-by-step what was done + verification procedures
```

---

## Key Points

✨ **Runtime Detection Works Like This:**
1. Assembly loaded → gets TargetFrameworkAttribute
2. Parse ".NETCoreApp,Version=v10.0" → extract "10.0"
3. Look for `FSharp.Core.SurfaceArea.net10.0.debug.bsl` ✓

📦 **Backward Compatibility:**
- If net10 baseline missing, falls back to netstandard21
- Old netcore IL baselines still work as fallback
- No breaking changes to non-net10 test runs

🔄 **Test Behavior:**
- `#if NETCOREAPP` (compile-time) → used for net10/netcore vs net472 split
- Runtime TFM detection → picks specific net10.0/net9.0/etc. baselines

---

## Questions?

Check:
1. `docs/NET10_BSL_MIGRATION.md` — Full technical details
2. `docs/BSL_TESTS.md` — Original BSL framework documentation
3. `eng/TargetFrameworks.props` — Default TFM settings
