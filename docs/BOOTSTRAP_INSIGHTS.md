# F# Bootstrap - Discovered Insights (net10.0 Migration)

**Author**: AI Assistant  
**Date**: April 15, 2026  
**Context**: Debugging bootstrap failure when upgrading to net10.0 with runtime-specific optimizations

---

## Problem Statement

After upgrading fsc.fsproj to net10.0 and adding runtime-specific optimizations (`UseAppHost`, `RuntimeIdentifier`, `AppendRuntimeIdentifierToOutputPath`), the Proto (bootstrap) build failed with runtime errors (exit code 131) when the compiler tried to execute itself.

---

## Root Causes Discovered

### 1. **Runtime Configuration File Missing from Bootstrap**

**Issue**: Proto publish created DLLs but no `fsc.runtimeconfig.json`. When MSBuild tried to invoke the bootstrap `fsc.dll` as a tool, .NET runtime couldn't find its configuration and crashed.

**Error**:
```
A fatal error was encountered. The library 'libhostpolicy.so' required to execute 
the application was not found. Failed to run as a self-contained app.
```

**Root Cause**: `OutputType=Library` prevents generation of `runtimeconfig.json`. Libraries don't need runtime config; executables do.

**Solution**: Make `OutputType` conditional:
```xml
<OutputType Condition="'$(Configuration)' == 'Proto'">Exe</OutputType>
<OutputType Condition="'$(Configuration)' != 'Proto'">Library</OutputType>
```

**Why This Works**:
- Proto builds compile fsc as an **executable** → generates `fsc.exe` + `fsc.runtimeconfig.json`
- Release/Debug builds compile fsc as a **library** → generates `fsc.dll` (no runtime config needed for referencing)
- MSBuild/Arcade can then locate and execute the bootstrap compiler with proper runtime environment

---

### 2. **Unconditional Runtime Optimizations Break Proto**

**Issue**: Properties like `UseAppHost`, `PublishReadyToRun`, `RuntimeIdentifier` were set unconditionally, causing Proto builds to attempt runtime-specific optimizations without a target runtime.

**Errors**:
```
NETSDK1094: Unable to optimize assemblies for performance: a valid runtime 
package was not found. Either set the PublishReadyToRun property to false, 
or use a supported runtime identifier when publishing.
```

**Root Cause**: Proto builds use the .NET SDK's built-in F# compiler, which doesn't have runtime identity yet. Trying to enable `PublishReadyToRun` without `RuntimeIdentifier` fails.

**Solution**: Conditionals prevent Proto from using Release-only optimizations:
```xml
<PublishReadyToRun Condition="'$(Configuration)' != 'Proto'">$(EnablePublishReadyToRun)</PublishReadyToRun>
<RuntimeIdentifier Condition="'$(Configuration)' != 'Proto'">$(NETCoreSdkRuntimeIdentifier)</RuntimeIdentifier>
<AppendRuntimeIdentifierToOutputPath Condition="'$(Configuration)' != 'Proto'">false</AppendRuntimeIdentifierToOutputPath>
<GenerateRuntimeConfigurationFiles Condition="'$(Configuration)' != 'Proto'">true</GenerateRuntimeConfigurationFiles>
<UseAppHost Condition="'$(Configuration)' != 'Proto'">true</UseAppHost>
```

**Why This Matters**:
- Proto = "prototype" / "bootstrap" phase uses generic compiler
- Release/Debug = "real" phase uses optimized/platform-specific compiler
- These are fundamentally different build scenarios; properties must reflect that

---

### 3. **Compilation Order of Included Sources**

**Issue**: `FSharp.Build.fsproj` included `FSharp.Compiler.Interop` source files AFTER files that tried to `open FSharp.Compiler.Interop`, causing "namespace not defined" errors.

**Error**:
```
error FS0039: The namespace 'Compiler' is not defined.
```

**Root Cause**: F# compiler processes files in order. If `Fsc.fs` (line 10: `open FSharp.Compiler.Interop`) appeared before the Interop files in the `<Compile>` ItemGroup, the namespace wasn't available yet.

**Solution**: Reorder `<Compile>` items to put Interop files first:
```xml
<ItemGroup>
  <!-- These must come first, before any file that opens the namespace -->
  <Compile Include="..\FSharp.Compiler.Interop\String.fs" />
  <Compile Include="..\FSharp.Compiler.Interop\Path.fs" />
  <!-- ... other Interop files ... -->
  
  <!-- Now files that depend on Interop -->
  <Compile Include="FSharpCommandLineBuilder.fs" />
  <Compile Include="Fsc.fs" />
</ItemGroup>
```

**Why This Matters**:
- Unlike project references (which MSBuild resolves automatically), inline source files require explicit order
- This is a common gotcha when breaking up monolithic projects into distributed source files
- The MSBuild XML Item ordering is significant and deterministic

---

### 4. **net10.0 Generic Constraints: `not null` Requirement**

**Issue**: `Dictionary<'K, 'V>` in net10 (with nullable reference types) requires `'K: not null`, but code only specified `'K: equality`.

**Error**:
```
error FS0001: A type parameter is missing a constraint 'when 'K: not null'
```

**Root Cause**: .NET 10 tightened nullable semantics. BCL types like `Dictionary` now explicitly forbid null keys at the type level (more strict than earlier .NET versions).

**Solution**: Add the constraint:
```fsharp
let createDictionary<'K, 'V when 'K: equality and 'K: not null> ...
let addToDictionary ... when 'K: equality and 'K: not null = ...
```

**Why This Matters**:
- This is a breaking change from earlier .NET versions where the constraint was implicit
- Any net10.0 code using generic collections must be explicit about nullability
- Similar issue affects `SortedDictionary<'K, 'V>` with `'K: comparison`

---

## Architecture Implications

### Configuration vs. Runtime Phases

The F# compiler bootstrap has **two distinct phases** that must be configured differently:

| Phase | Name | OutputType | RuntimeIdentifier | PublishReadyToRun | Purpose |
|-------|------|------------|-------------------|-------------------|---------|
| 1 | Proto | **Exe** | (empty) | **false** | Bootstrap: Use SDK compiler to build fsc, produces generic executable |
| 2 | Release/Debug | **Library** | $(NETCoreSdkRuntimeIdentifier) | **true** | Self-compilation: Use bootstrap to build optimized, platform-specific compiler |

**Lesson**: Build system configuration must distinguish between "bootstrapping" and "optimizing" phases. Properties aren't universally applicable; they're phase-dependent.

---

## Conditions as First-Class Build Semantics

The bootstrap uses MSBuild `Condition` attributes extensively:

```xml
<!-- Pattern: Only apply optimization in Release/Debug (not Proto) -->
<PropertyGroup Condition="'$(Configuration)' != 'Proto'">
  <!-- optimization properties here -->
</PropertyGroup>
```

**Key Insight**: `Condition` is not just a convenience; it's expressing **build phase semantics**. The condition `'$(Configuration)' != 'Proto'` means "apply this only in non-bootstrap builds."

**Best Practice**: Always document **why** a property is conditional, not just when. The why communicates intent to future maintainers.

---

## Generic Type System Changes Across .NET Versions

When targeting new framework versions, review:

1. **Nullable Reference Types (NRT)**: Changed nullability semantics
   - `'T: not null` constraint now meaningful and sometimes required
   
2. **Generic Constraints**: May be stricter in newer .NET SDKs
   - `Dictionary<'K, 'V>` now requires `'K: not null` in net10
   - `IDisposable | IAsyncDisposable` union now more strict

3. **Runtime Configuration**: Behavior changes with platform-specific builds
   - `RuntimeIdentifier` + `PublishReadyToRun` require coordination
   - Cannot enable one without validating the other is set

---

## Recommended Validation Steps for Future net+1 Upgrades

When upgrading to a new .NET version:

1. **Verify Proto Build Phase**:
   ```bash
   dotnet publish proto.proj -c Proto
   ls artifacts/Bootstrap/fsc/
   # Must contain: fsc.runtimeconfig.json, fsc.dll, Microsoft.FSharp.Targets
   ```

2. **Test Bootstrap Compiler Execution**:
   ```bash
   dotnet artifacts/Bootstrap/fsc/fsc.dll --version
   # Should display version without "fatal error"
   ```

3. **Check Generic Constraints**:
   - Search for `Dictionary<'K, 'V>` and `SortedDictionary<'K, 'V>`
   - Ensure constraints include `'K: not null` if targeting net10+

4. **Validate Runtime Configuration**:
   - All conditional properties on runtime must have `Condition="'$(Configuration)' != 'Proto'"`

---

## Build System Tension: MSBuild vs. Bash vs. Pure F#

**Current Approach**:
- `build.sh` (bash) orchestrates
- `proto.proj` (MSBuild XML) defines bootstrap
- `FSharpBuild.Directory.Build.props` (MSBuild XML) applies redirection

**Pros**:
- Standard .NET tooling (everyone knows MSBuild)
- Works across Windows/Mac/Linux with bash+dotnet

**Cons**:
- XML verbosity makes logic hard to follow
- Bash scripts fragile (string parsing, error handling)
- Conditions scattered across multiple files (props, targets, fsproj)
- No type safety; errors at build time, not parse time

**Insight**: The bootstrap logic is complex enough to warrant a real language. See: `BOOTSTRAP_FAKE_SPEC.md` for proposed improvement.

---

## Files Modified in This Fix

1. **src/fsc/fscProject/fsc.fsproj**
   - Made `PublishReadyToRun`, `RuntimeIdentifier`, `AppendRuntimeIdentifierToOutputPath`, `GenerateRuntimeConfigurationFiles`, `UseAppHost` conditional on `Configuration != Proto`

2. **src/fsc/fsc.targets**
   - Made `OutputType` conditional: `Exe` for Proto, `Library` for Release/Debug

3. **src/FSharp.Build/FSharp.Build.fsproj**
   - Reordered `<Compile>` items to place `FSharp.Compiler.Interop` files first

4. **src/FSharp.Compiler.Interop/Collections.fs**
   - Added `and 'K: not null` constraint to `createDictionary` and `createSortedDictionary`

---

## References

- [BOOTSTRAP_QUICK_REFERENCE.md](BOOTSTRAP_QUICK_REFERENCE.md) - Quick lookup for bootstrap flow
- [BOOTSTRAP_GUIDE.md](BOOTSTRAP_GUIDE.md) - Detailed bootstrap walkthrough
- [BOOTSTRAP_FILES_REFERENCE.md](BOOTSTRAP_FILES_REFERENCE.md) - File location reference
- [BOOTSTRAP_FAKE_SPEC.md](BOOTSTRAP_FAKE_SPEC.md) - Proposed FAKE build system redesign
