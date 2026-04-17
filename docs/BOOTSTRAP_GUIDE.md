# F# Compiler Bootstrap - Complete Technical Guide

## TL;DR - The Secret

F# doesn't need F# to compile itself because:
1. **Stage 1 (Proto/Bootstrap)**: Uses .NET SDK's **built-in F# compiler** (from `Sdk="Microsoft.NET.Sdk"`)
2. **Stage 2 (Real)**: Uses the freshly-built bootstrap compiler via **compiler redirection** (FSharpTargetsPath)

No circular dependency. No pre-compiled binary needed. Just the .NET SDK.

---

## Quick Reference: Key Conditions

| Condition | When Proto Build | When Release Build | Effect |
|-----------|-----------------|-------------------|--------|
| `Configuration` | `Proto` | `Release` / `Debug` | ← Toggles all behavior |
| `DisableCompilerRedirection` | empty (not set) | empty | See ProtoOutputPath check |
| `ProtoOutputPath exists` | N/A | YES | Activates FSharpTargetsPath redirection |
| `FSharpTargetsPath set` | NO | YES | Redirects to bootstrap compiler |
| `BUILDING_WITH_LKG` | YES | NO | Sometimes used in source code |
| `DisableImplicitFSharpCoreReference` | YES (tools) | NO | Tools don't auto-reference FSharp.Core |

---

## Full Bootstrap Architecture

```
START: ./build.sh -c Release --tfm 10.0
  │
  ├─ eng/build.sh
  │   │
  │   ├─ Check: artifacts/Bootstrap/fslex/fslex.dll exists?
  │   │
  │   └─ NO? → STAGE 2: Proto Bootstrap Build
  │       │
  │       ├─ dotnet publish proto.proj /p:Configuration=Proto
  │       │
  │       ├─ File: proto.proj lists 5 projects:
  │       │   ├─ buildtools/fslex/fslex.fsproj
  │       │   ├─ buildtools/fsyacc/fsyacc.fsproj
  │       │   ├─ buildtools/AssemblyCheck/AssemblyCheck.fsproj
  │       │   ├─ src/fsc/fscProject/fsc.fsproj
  │       │   └─ src/fsi/fsiProject/fsi.fsproj
  │       │
  │       ├─ During build:
  │       │   ├─ Condition check in FSharpBuild.Directory.Build.props:
  │       │   │   '$(Configuration)' != 'Proto'  ← FALSE! Condition fails
  │       │   │   → FSharpTargetsPath NOT set
  │       │   │   → SDK's built-in F# compiler IS used
  │       │   │
  │       │   ├─ All .fsproj files use Sdk="Microsoft.NET.Sdk"
  │       │   │   (This imports SDK's F# targets)
  │       │   │
  │       │   └─ Tools have DisableImplicitFSharpCoreReference=true
  │       │       (So they don't depend on FSharp.Core during proto build)
  │       │
  │       └─ Output to: artifacts/Bootstrap/{fslex,fsyacc,fsc,fsi}/
  │           ├─ fslex/fslex.dll
  │           ├─ fsc/fsc.dll
  │           ├─ fsc/FSharp.Build.dll
  │           ├─ fsc/Microsoft.FSharp.Targets    ← KEY!
  │           ├─ fsc/Microsoft.FSharp.NetSdk.props
  │           └─ fsc/Microsoft.FSharp.NetSdk.targets
  │
  └─ STAGE 3: Real Compiler Build
      │
      ├─ MSBuild FSharp.sln /p:Configuration=Release
      │
      ├─ During build:
      │   ├─ Condition check in FSharpBuild.Directory.Build.props:
      │   │   '$(Configuration)' != 'Proto'      ← TRUE!
      │   │   '$(DisableCompilerRedirection)'!='true'  ← TRUE! (defaults empty)
      │   │   Exists('$(ProtoOutputPath)')       ← TRUE! (Bootstrap succeeded)
      │   │   → Condition PASSES ✓
      │   │
      │   ├─ FSharpTargetsPath = artifacts/Bootstrap/fsc/Microsoft.FSharp.Targets
      │   ├─ FSharpPropsShim = artifacts/Bootstrap/fsc/Microsoft.FSharp.NetSdk.props
      │   ├─ FSharpTargetsShim = artifacts/Bootstrap/fsc/Microsoft.FSharp.NetSdk.targets
      │   │
      │   ├─ SDK's FSharpTargetsShim property observes these and:
      │   │   → Loads Microsoft.FSharp.Targets from Bootstrap!
      │   │   → ALL subsequent F# compilation uses bootstrap's fsc.dll
      │   │
      │   └─ Result: Self-compiling F# compiler! 🎉
      │
      └─ Output to: artifacts/bin/{fsc,fsi,FSharp.Build,FSharp.Core,...}*/
          Complete F# compiler, built by itself
```

---

## The 5 Secret Files (Hidden Mechanisms)

### 1. **Sdk="Microsoft.NET.Sdk"** (Implicit in every .fsproj)
- **What**: Every fsproj declares this SDK in `<Project Sdk=...>`
- **Effect**: Imports Microsoft's F# build targets from installed .NET SDK
- **Why important**: This gives us a FREE F# compiler during Proto build
- **Cost**: None. Part of .NET SDK installation

### 2. **FSharpBuild.Directory.Build.props** (Lines 106-109)
```xml
<PropertyGroup Condition="'$(Configuration)' != 'Proto' AND 
                          '$(DisableCompilerRedirection)'!='true' AND 
                          Exists('$(ProtoOutputPath)')">
    <FSharpTargetsPath>$(ProtoOutputPath)\fsc\Microsoft.FSharp.Targets</FSharpTargetsPath>
    <FSharpPropsShim>$(ProtoOutputPath)\fsc\Microsoft.FSharp.NetSdk.props</FSharpPropsShim>
    <FSharpTargetsShim>$(ProtoOutputPath)\fsc\Microsoft.FSharp.NetSdk.targets</FSharpTargetsShim>
</PropertyGroup>
```
- **What**: Controls compiler redirection
- **When active**: Release/Debug builds with successful bootstrap
- **Effect**: Hijacks where MSBuild loads F# targets from
- **Key**: `Exists('$(ProtoOutputPath)')` gates the hijacking

### 3. **buildtools/fslex/fslex.fsproj** & **buildtools/fsyacc/fsyacc.fsproj**
```xml
<DisableImplicitFSharpCoreReference>true</DisableImplicitFSharpCoreReference>
```
- **What**: Prevents automatic FSharp.Core reference
- **Why**: During bootstrap, FSharp.Core isn't ready yet
- **Solution**: These tools implement their own Lexing/Parsing primitives
- **Location**: See `Lexing.fs`, `Parsing.fs`, `Arg.fs` inside each tool

### 4. **FSharp.Profiles.props** (Lines 5-7)
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Proto'">
    <DefineConstants>BUILDING_WITH_LKG;$(DefineConstants)</DefineConstants>
</PropertyGroup>
```
- **What**: Defines `BUILDING_WITH_LKG` during Proto builds
- **Usage**: Source files can check `#if BUILDING_WITH_LKG`
- **Purpose**: Allows bootstrap-specific workarounds in code

### 5. **proto.proj** (Root directory)
```xml
<ItemGroup>
    <Projects Include="buildtools\fslex\fslex.fsproj" />
    <Projects Include="buildtools\fsyacc\fsyacc.fsproj" />
    <Projects Include="buildtools\AssemblyCheck\AssemblyCheck.fsproj" />
    <Projects Include="src\fsc\fscProject\fsc.fsproj" />
    <Projects Include="src\fsi\fsiProject\fsi.fsproj" />
</ItemGroup>
<Target Name="Publish">
    <!-- Publish these 5 to artifacts/Bootstrap -->
</Target>
```
- **What**: Defines WHAT gets bootstrapped
- **How**: Uses MSBuild to publish all 5 projects
- **Output**: `artifacts/Bootstrap/{name}/` for each project
- **Key**: Only these 5 projects in the bootstrap phase

---

## Property Reference Table

| Property | Default | Set By | Scope | Notes |
|----------|---------|--------|-------|-------|
| `Configuration` | `Debug` | `--configuration` flag | Global | Controls Proto vs Release path |
| `ProtoOutputPath` | `artifacts/Bootstrap` | FSharpBuild.Directory.Build.props | Global | Where bootstrap output goes |
| `FSharpNetCoreProductTargetFramework` | `net10.0` | eng/TargetFrameworks.props | Global | Target .NET version for products |
| `DisableCompilerRedirection` | (empty) | proto.proj / MSBuild | Proto stage | Set empty = use .NET SDK compiler |
| `BUILDING_WITH_LKG` | (undefined) | FSharp.Profiles.props | Proto stage only | Compile-time define when Proto |
| `FSharpTargetsPath` | (empty) | FSharpBuild.Directory.Build.props | Release/Debug | Path to Microsoft.FSharp.Targets |
| `FSharpPropsShim` | (empty) | FSharpBuild.Directory.Build.props | Release/Debug | Path to NetSdk.props |
| `FSharpTargetsShim` | (empty) | FSharpBuild.Directory.Build.props | Release/Debug | Path to NetSdk.targets |
| `DisableImplicitFSharpCoreReference` | `false` | buildtools/*.fsproj | Tools only | Prevents circular ref in bootstrap |
| `FSHARPCORE_USE_PACKAGE` | `true` or `false` | fsc.targets, Directory.Build.props | Global | Use NuGet FSharp.Core vs local? |

---

## Walkthrough: What Happens When You Run `./build.sh -c Release`

### Step 1: Entry Point
```bash
$ ./build.sh -c Release --tfm 10.0
↓
source eng/build.sh --restore --build -c Release --tfm 10.0
```

### Step 2: Parse Arguments
```bash
# eng/build.sh processes:
configuration="Release"
build=true
restore=true  (implicit with --build)
tfm="10.0"
```

### Step 3: Initialize Toolset
```bash
InitializeDotNetCli
# Gets .NET SDK, sets DOTNET_INSTALL_DIR
```

### Step 4: Check Bootstrap Exists
```bash
bootstrap_dir=$artifacts_dir/Bootstrap  # artifacts/Bootstrap
if [ ! -f "$bootstrap_dir/fslex/fslex.dll" ]; then
    # Bootstrap needed!
```

### Step 5a: Build Proto (Bootstrap) - If Needed
```bash
dotnet publish proto.proj \
    /restore \
    /p:Configuration=Proto \
    /p:DotNetBuild=false \
    /p:DotNetBuildSourceOnly=false
```

**During this publish:**
1. MSBuild processes proto.proj
2. For each project (fslex, fsyacc, fsc, fsi, ...):
   a. Load all .fsproj files
   b. Import Directory.Build.props (sets properties)
   c. Condition check: `'$(Configuration)' != 'Proto'` → **FALSE**
   d. FSharpTargetsPath NOT set
   e. Sdk="Microsoft.NET.Sdk" imported → **uses SDK's F# compiler** ✓
   f. Compile & publish to artifacts/Bootstrap/{name}/

### Step 5b: Verify Bootstrap Output
```bash
bootstrap_dir/fslex/fslex.dll ✓
bootstrap_dir/fsc/fsc.dll ✓
bootstrap_dir/fsc/Microsoft.FSharp.Targets ✓  ← KEY FILE!
```

### Step 6: Build Real Compiler (Release)
```bash
MSBuild FSharp.sln \
    /p:Configuration=Release \
    /p:Projects="FSharp.sln"
```

**During this MSBuild:**
1. Load projects from FSharp.sln
2. Directory.Build.props evaluation:
   - `'$(Configuration)' != 'Proto'` → **TRUE** ✓
   - `'$(DisableCompilerRedirection)'!='true'` → **TRUE** ✓
   - `Exists('$(ProtoOutputPath)')` → **TRUE** ✓
   - **All conditions pass** → Set FSharpTargetsPath, FSharpPropsShim, FSharpTargetsShim

3. SDK's FSharpTargetsShim loads from:
   ```
   artifacts/Bootstrap/fsc/Microsoft.FSharp.Targets
   ```

4. All F# compilation now uses:
   ```
   artifacts/Bootstrap/fsc/fsc.dll  ← Our newly-built bootstrap!
   artifacts/Bootstrap/fsc/FSharp.Build.dll
   ```

5. Projects build in order:
   - FSharp.Core (already built in proto as prerequisite)
   - FSharp.Compiler.Service (compiles itself!)
   - FSharp.Build
   - fsc.exe
   - fsi.exe
   - ... all other projects

### Step 7: Output
```
artifacts/bin/
├── fsc/Release/net10.0/
│   ├── fsc.dll (NEW: self-compiled)
│   ├── fsc.exe
│   └── (all dependencies)
├── fsi/Release/net10.0/
│   ├── fsi.exe (NEW: self-compiled)
│   └── (all dependencies)
├── FSharp.Build/Release/
│   └── FSharp.Build.dll (NEW: self-compiled)
├── FSharp.Compiler.Service/Release/
│   └── FSharp.Compiler.Service.dll (NEW: self-compiled!)
└── FSharp.Core/Release/
    └── FSharp.Core.dll (NEW: self-compiled)
```

### Step 8: (Optional) Run Tests
```bash
./build.sh -c Release --testcoreclr
# Runs tests using the newly-built compiler
```

---

## Debugging Bootstrap Failures

### Symptom: Proto build fails
```
$ ./build.sh -c Release
...
Publishing proto.proj with /p:Configuration=Proto
ERROR: ...
```

**Causes:**
1. `.NET SDK not installed`: Install latest stable .NET SDK
2. `Network issues`: Can't reach NuGet feeds in NuGet.config
3. `Corrupted artifacts`: Run `dotnet clean && rm -rf artifacts/`
4. `MSBuild issues`: Try `dotnet publish --help` to diagnose

**Fix:**
```bash
rm -rf artifacts/Bootstrap  # Force bootstrap rebuild
./build.sh -c Release --bootstrap
```

### Symptom: Real build fails using bootstrap
```
Building FSharp.sln with /p:Configuration=Release
ERROR: fsc.dll not found or incompatible
```

**Causes:**
1. Bootstrap incomplete (missing files in artifacts/Bootstrap/fsc/)
2. FSharpTargetsPath not being set (check conditions in Directory.Build.props)
3. Corrupted FSharp.Targets or FSharp.Build.dll in bootstrap

**Fix:**
```bash
# Option 1: Force rebuild of bootstrap
./build.sh --bootstrap -c Release

# Option 2: Disable redirection (use SDK compiler)
./build.sh -c Release /p:DisableCompilerRedirection=true

# Option 3: Full clean
rm -rf artifacts/
./build.sh -c Release
```

### Symptom: Can't reproduce issue
```bash
# Add binary log to see exact MSBuild operations
./build.sh -c Release --binarylog

# Check the binlog
artifacts/log/Build.binlog (view in MSBuild Binary Log Viewer)
Checking step 1: Look for FSharpTargetsPath property
Checking step 2: Look for which fsc.dll was loaded
```

---

## Advanced: Disabling Bootstrap Redirection

Sometimes you want to build WITHOUT the bootstrap compiler (e.g., first initial build):

```bash
./build.sh -c Release /p:DisableCompilerRedirection=true
```

**Effect:**
- Condition in FSharpBuild.Directory.Build.props:
  ```xml
  '$(DisableCompilerRedirection)'!='true'  ← NOW FALSE
  ```
- FSharpTargetsPath NOT set
- SDK's F# compiler is used instead
- Result: Uses .NET SDK's built-in compiler, NOT the bootstrap

**When to use:**
- Bootstrapping from scratch on clean machine
- Debugging SDK interactions
- Testing SDK compiler version compatibility

---

## Advanced: Custom Target Framework

Default target framework for products is `net10.0`. Override via CLI:

```bash
# Build for .NET 9
./build.sh -c Release /p:FSharpNetCoreProductTargetFramework=net9.0

# Build for .NET 8  
./build.sh -c Release /p:FSharpNetCoreProductTargetFramework=net8.0
```

This affects:
- Which .NET SDK fsc.dll is used for proto build
- Output TFM in artifacts/bin/{name}/Release/{tfm}/
- Proto compiler's target runtime

---

## Key Takeaways

1. **No Magic Binary**: No pre-compiled F# compiler binary in the repo
2. **SDK is Key**: .NET SDK's built-in F# compiler bootstraps the process
3. **Two Stages**: Proto (using SDK) → Real (using bootstrap)
4. **Redirection Mechanism**: FSharpTargetsPath and friends hijack MSBuild's compiler choice
5. **Conditional Logic**: `Configuration` property controls everything
6. **Gate Condition**: `Exists('$(ProtoOutputPath)')` gates the redirection
7. **Tools Isolation**: buildtools don't reference FSharp.Core during bootstrap
8. **Self-Compiling**: Once bootstrap succeeds, F# compiles itself

---

## Related Documentation

- **SDK Integration**: See https://github.com/dotnet/sdk/tree/main/src/Tasks/Microsoft.NET.Build.Tasks/targets
- **Arcade Build System**: https://github.com/dotnet/arcade (imported but disabled in `Directory.Build.props` via `DISABLE_ARCADE`)
- **F# Compiler Development**: See DEVGUIDE.md in repo root
- **.NET SDK F#**: https://github.com/dotnet/sdk/tree/main/src/Sdk/Sdk/Sdk.props
