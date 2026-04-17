# F# Bootstrap Quick Reference Card

## The Secret (2 sentences)
The .NET SDK includes a built-in F# compiler. During `Configuration=Proto` builds, this SDK compiler bootstraps the F# repository's own compiler. Then, conditional MSBuild properties redirect subsequent builds to use the bootstrapped compiler instead.

---

## Quick Flow

```
./build.sh -c Release
    ↓
[Check: artifacts/Bootstrap exists?]
    ├─ NO → Run proto.proj with Configuration=Proto
    │       └─ Uses .NET SDK's F# compiler
    │       └─ Output → artifacts/Bootstrap/
    │
    └─ YES → Skip bootstrap
    
    ↓
[Build FSharp.sln with Configuration=Release]
    ├─ FSharpBuild.Directory.Build.props condition passes (Configuration != Proto)
    ├─ FSharpTargetsPath = artifacts/Bootstrap/fsc/Microsoft.FSharp.Targets
    ├─ All F# compilation now uses BOOTSTRAP compiler
    └─ Output → artifacts/bin/
```

---

## Key Property: Configuration

| Value | Stage | Compiler Used | What Gets Built |
|-------|-------|---------------|-----------------|
| `Proto` | Bootstrap | .NET SDK's built-in fsc.dll | fslex, fsyacc, fsc.exe, fsi.exe |
| `Debug`/`Release` | Real | artifacts/Bootstrap/fsc/fsc.dll | Everything |

---

## Critical Conditions (ALL must pass to activate redirection)

```xml
<!-- File: FSharpBuild.Directory.Build.props line 106 -->
<PropertyGroup Condition="'$(Configuration)' != 'Proto' AND 
                          '$(DisableCompilerRedirection)'!='true' AND 
                          Exists('$(ProtoOutputPath)')">
```

✅ **Configuration != Proto** - We're building Release/Debug, not bootstrap  
✅ **DisableCompilerRedirection != true** - Redirection not explicitly disabled  
✅ **ProtoOutputPath exists** - Bootstrap build already succeeded  

**When ALL true**: Redirection activates → Use bootstrap compiler  
**When any false**: Redirection skipped → Use .NET SDK compiler

---

## Important Files

| File | Purpose | Key Line(s) |
|------|---------|-------------|
| `build.sh` | User entry | Calls `eng/build.sh` |
| `eng/build.sh` | Orchestration | Line ~316: Bootstrap check |
| `proto.proj` | Bootstrap definition | Lists 5 projects to compile |
| `FSharpBuild.Directory.Build.props` | Redirection config | Line 106: Condition + FSharpTargetsPath |
| `buildtools/fslex/fslex.fsproj` | Lexer tool | Line 4: DisableImplicitFSharpCoreReference |
| `buildtools/fsyacc/fsyacc.fsproj` | Parser tool | Line 4: DisableImplicitFSharpCoreReference |
| All `.fsproj` files | Implicit | Line 1: `Sdk="Microsoft.NET.Sdk"` |

---

## Hidden Mechanisms (The Secrets!)

### 1. Sdk="Microsoft.NET.Sdk"
Implies F# compiler in installed .NET SDK. Implicit in every .fsproj.

### 2. Configuration Property Switch
Controls Proto vs Real compiler selection.

### 3. ProtoOutputPath Existence Check
Gates whether redirection is enabled.

### 4. FSharpTargetsPath Hijacking
Tells MSBuild where to load F# targets from (SDK vs Bootstrap).

### 5. DisableImplicitFSharpCoreReference
Breaks circular dependency in bootstrap stage.

### 6. BUILDING_WITH_LKG Define
Allows source code to detect bootstrap stage.

---

## Common Tasks

### Full rebuild from scratch
```bash
rm -rf artifacts/
./build.sh -c Release
```

### Force re-bootstrap
```bash
./build.sh --bootstrap -c Release
```

### Build without bootstrap (use SDK compiler)
```bash
./build.sh -c Release /p:DisableCompilerRedirection=true
```

### Build for .NET 9 instead of net10.0
```bash
./build.sh -c Release /p:FSharpNetCoreProductTargetFramework=net9.0
```

### Run tests after build
```bash
./build.sh -c Release --testcoreclr
```

### Debug: View what compiler is being used
Look in artifacts/ folder:
```
artifacts/Bootstrap/fsc/  ← Bootstrap (proto) compiler output
artifacts/bin/fsc/Release/  ← Real compiler (built by bootstrap)
```

---

## Troubleshooting Checklist

- [ ] .NET SDK installed? `dotnet --version`
- [ ] Bootstrap exists? `ls artifacts/Bootstrap/fslex/fslex.dll`
- [ ] FSharpTargetsPath being set? Add `/bl` flag, check binlog
- [ ] Corrupted artifacts? Try `rm -rf artifacts/ && ./build.sh -c Release`
- [ ] Network issues? Check NuGet.config sources are accessible
- [ ] MSBuild errors? Try `/p:DisableCompilerRedirection=true` as fallback

---

## Property Reference (Quick Lookup)

| Property | Set To | When | By | If Missing → |
|----------|--------|------|----|-|
| Configuration | Proto | Bootstrap | CLI (`--configuration Proto`) | Uses Debug |
| Configuration | Release | Real compiler | CLI (`-c Release`) | Uses Debug |
| ProtoOutputPath | artifacts/Bootstrap | Always | FSharpBuild.Directory.Build.props line 82 | Bootstrap not collected |
| FSharpTargetsPath | $(ProtoOutputPath)\fsc\Microsoft.FSharp.Targets | Release/Debug only | Line 107 (conditional) | Uses SDK compiler |
| DisableImplicitFSharpCoreReference | true | Tools only | buildtools/*.fsproj line 4 | Circular dependency |
| BUILDING_WITH_LKG | true | Proto build | FSharp.Profiles.props line 6 | Conditional code not used |
| DisableCompilerRedirection | empty | Always | (Default) | Redirection enabled |

---

## The Build DAG (Dependency Graph)

```
.NET SDK
    ↓
[Proto Build - Configuration=Proto]
    ├─ fslex (depends on SDK compiler)
    ├─ fsyacc (depends on SDK compiler)  
    ├─ fsc.exe (depends on SDK compiler)
    └─ fsi.exe (depends on SDK compiler)
    ↓
artifacts/Bootstrap/ ← Redirection target
    ↓
[Real Build - Configuration=Release/Debug]
    ├─ FSharp.Core (depends on Bootstrap/fsc)
    ├─ FSharp.Compiler.Service (depends on Bootstrap/fsc + FSharp.Core)
    ├─ FSharp.Build (depends on bootstrap)
    ├─ fsc.exe (depends on bootstrap to rebuild itself!)
    └─ fsi.exe (depends on bootstrap to rebuild itself!)
    ↓
artifacts/bin/ ← Self-compiled F# compiler!
```

---

## One Pager: What Happens When

| Event | File Involved | Key Property | Behavior |
|-------|:-------------:|:------------:|----------|
| `./build.sh` runs | eng/build.sh | N/A | Checks if bootstrap exists |
| bootstrap missing | proto.proj | Configuration=Proto | Uses SDK compiler, outputs to Bootstrap/ |
| bootstrap exists | FSharpBuild.Directory.Build.props | Configuration != Proto | Activates redirection |
| redirection active | All .fsproj | FSharpTargetsPath set | Uses Bootstrap/fsc for all F# builds |
| FSharp.Core builds | src/FSharp.Core/FSharp.Core.fsproj | Depends on fsc | Uses bootstrap to compile itself |
| fsc.exe rebuilds | src/fsc/fscProject/fsc.fsproj | Depends on FSharp.Core | Uses bootstrap (that was built from bootstrap!) |

---

## FAQ

**Q: Why not just ship a bootstrap compiler?**  
A: Reduces repo size, avoids binary maintenance, leverages .NET SDK's investments.

**Q: What if ProtoOutputPath doesn't exist?**  
A: Redirection condition fails, SDK compiler is used for everything. (Slower, less tested path.)

**Q: Can I force use of SDK compiler?**  
A: Yes: `/p:DisableCompilerRedirection=true`

**Q: What if bootstrap build fails?**  
A: Check .NET SDK version, network access to NuGet, disk space. Try `rm -rf artifacts/` and retry.

**Q: Does this work offline?**  
A: Only if you're on the EXACT same .NET SDK version. Different SDKs may have different F# versions.

**Q: Which F# version does SDK ship?**  
A: Matches the .NET SDK version (e.g., .NET 10 ships with F# 10).

---

## Visual: Proto Build Stage

```
.NET SDK's fsc.dll
        ↓ (compiles)
    [fslex.fs]
        ↓ (output)
artifacts/Bootstrap/fslex/fslex.dll
        ↓
    [fsyacc.fs]
        ↓
artifacts/Bootstrap/fsyacc/fsyacc.dll
        ↓
    [fsc source code]
        ↓
artifacts/Bootstrap/fsc/fsc.dll ← BOOTSTRAP COMPILER BORN!
artifacts/Bootstrap/fsc/Microsoft.FSharp.Targets ← KEY!
```

---

## Visual: Real Build Stage (Self-Compilation)

```
Bootstrap fsc.dll (from ProtoOutputPath)
        ↓ (via FSharpTargetsPath redirection)
    [FSharp.Core source]
        ↓
artifacts/bin/FSharp.Core.dll
        ↓
    [FSharp.Compiler.Service source]
        ↓
artifacts/bin/FSharp.Compiler.Service.dll
        ↓
    [fsc fresh source] ← Uses a version of itself!
        ↓
artifacts/bin/fsc/fsc.exe ← NEW: SELF-COMPILED!
```

---

## When Proto Build is Skipped

**Condition**: `[ -f "$bootstrap_dir/fslex/fslex.dll" ]` is true

```
Pro: Faster! No need to rebuild bootstrap every time
Con: Must have succeeded at least once before

Recovery: ./build.sh --bootstrap -c Release  (forces rebuild)
```

---

## The 3-Minute Version

1. **You run**: `./build.sh -c Release`
2. **What happens**: 
   - Check if `artifacts/Bootstrap/fslex/fslex.dll` exists
   - If NO: Run `proto.proj` with `Configuration=Proto` (uses .NET SDK's F# compiler)
   - If YES: Skip bootstrap
3. **Then**: Run `FSharp.sln` with `Configuration=Release`
   - Condition in `FSharpBuild.Directory.Build.props` checks Configuration != Proto
   - If true: Set `FSharpTargetsPath` to bootstrap location
   - Result: All F# builds now use Bootstrap/fsc instead of SDK
4. **Output**: F# compiler built by itself in `artifacts/bin/fsc/`
