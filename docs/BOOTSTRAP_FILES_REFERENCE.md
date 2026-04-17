# F# Bootstrap - File Location Reference

## Master Control Files

### Entry Point: build.sh
**Path**: `build.sh`  
**Purpose**: Linux/macOS entry point  
**Key Step**: Line ~18 - Sources `eng/build.sh`

```bash
source="${BASH_SOURCE[0]}"
# resolve $source until the file is no longer a symlink
# ...
scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
"$scriptroot/eng/build.sh" --restore --build "$@"
```

---

### Main Orchestrator: eng/build.sh
**Path**: `eng/build.sh`  
**Lines**: Full file is the orchestration engine  

#### Key Sections:

**Lines 11-45**: Usage documentation
- Describes all build flags including `--bootstrap`

**Line 71**: Configuration default
```bash
configuration="Debug"
```

**Line 74**: Force bootstrap flag
```bash
force_bootstrap=false
```

**Lines 108-122**: Argument parsing
- Extracts `--configuration`, `--bootstrap`, `--tfm`, etc.

**Lines 250-270**: Test() function
- `testproject`, `targetframework` parameters
- Configures xunit/MTP test runner

**Lines 310-330**: **BOOTSTRAP BUILD** ← THE CRITICAL SECTION
```bash
# build bootstrap tools
# source_build=In source build proto does no work, except cause sourcebuild in wrapper to build
bootstrap_dir=$artifacts_dir/Bootstrap
if [[ "$force_bootstrap" == true ]]; then
    rm -fr $bootstrap_dir
fi
if [ ! -f "$bootstrap_dir/fslex/fslex.dll" ]; then
    # ... calls: dotnet publish proto.proj /p:Configuration=Proto
fi
```

**Lines 344-360**: Real compiler build
```bash
# do real build
BuildMessage="Error building solution"
MSBuild $toolset_build_proj \
  /p:Configuration=$configuration \
  /p:Projects="$projects" \
  ...
```

---

### Bootstrap Definition: proto.proj
**Path**: `proto.proj`  
**Lines**: 1-32 (entire file)  

**Lines 1-4**: Project root
```xml
<Project>
  <PropertyGroup>
    <RootDir Condition="'$(RootDir)'==''">Bootstrap</RootDir>
  </PropertyGroup>
```
- `RootDir` determines subfolder in `artifacts/`

**Lines 6-11**: Projects to bootstrap
```xml
<ItemGroup>
    <Projects Include="buildtools\fslex\fslex.fsproj" />
    <Projects Include="buildtools\fsyacc\fsyacc.fsproj" />
    <Projects Include="buildtools\AssemblyCheck\AssemblyCheck.fsproj" />
    <Projects Include="src\fsc\fscProject\fsc.fsproj" />
    <Projects Include="src\fsi\fsiProject\fsi.fsproj" />
</ItemGroup>
```

**Lines 13-35**: MSBuild targets (Build, Rebuild, Clean, Restore, Publish)
```xml
<Target Name="Publish">
    <MSBuild Projects="@(Projects)" Targets="Publish" 
             Properties="Configuration=$(Configuration);
                        DisableCompilerRedirection=$(DisableCompilerRedirection);
                        PublishDir=$(MSBuildThisFileDirectory)/artifacts/$(RootDir)/%(Filename)" />
</Target>
```

**Key**: `DisableCompilerRedirection` passed but NOT overridden during proto build → defaults empty → .NET SDK compiler used

---

## Property Control Files

### Root Properties: Directory.Build.props
**Path**: `Directory.Build.props`  
**Lines**: ~100 lines

**Lines 20-25**: FSharp.Compiler.Service solution special handling
```xml
<PropertyGroup Condition="'$(SolutionName)' == 'FSharp.Compiler.Service' and '$(BUILDING_USING_DOTNET)' != 'false'">
    <BUILDING_USING_DOTNET>true</BUILDING_USING_DOTNET>
</PropertyGroup>
```

**Lines 45-48**: BUILDING_WITH_LKG definition
```xml
<PropertyGroup Condition="'$(BUILDING_USING_DOTNET)' != 'true' and '$(Configuration)' != 'Proto'">
    <OtherFlags Condition="'$(BuildNoRealsig)' == 'true'">$(OtherFlags) --realsig-</OtherFlags>
    <OtherFlags Condition="'$(BuildNoRealsig)' != 'true'">$(OtherFlags) --realsig+</OtherFlags>
</PropertyGroup>
```

**Lines 60-85**: FSHARPCORE_USE_PACKAGE and DISABLE_ARCADE
```xml
<PropertyGroup Condition="'$(BUILDING_USING_DOTNET)' == 'true'">
    <DisableAutoSetFscCompilerPath>false</DisableAutoSetFscCompilerPath>
    <FSHARPCORE_USE_PACKAGE Condition="'$(FSHARPCORE_USE_PACKAGE)' == ''">true</FSHARPCORE_USE_PACKAGE>
    <DISABLE_ARCADE Condition="'$(DISABLE_ARCADE)' == ''">true</DISABLE_ARCADE>
    ...
fi
```

---

### Root Targets: Directory.Build.targets
**Path**: `Directory.Build.targets`  
**Lines**: ~40 lines (short file)

**Lines 1-3**: Imports
```xml
<Import Project="FSharpBuild.Directory.Build.targets" Condition = " '$(FSharpTestCompilerVersion)' == '' "/>
<Import Project="FSharpTests.Directory.Build.targets" Condition = " '$(FSharpTestCompilerVersion)' != '' "/>
```

---

### **PRIMARY REDIRECTION FILE**: FSharpBuild.Directory.Build.props
**Path**: `FSharpBuild.Directory.Build.props`  
**Lines**: ~130 lines  
**CRITICAL SECTION**: Lines 1-110

**Lines 1-5**: Arcade SDK import  
```xml
<Import Project="Sdk.props" Sdk="Microsoft.DotNet.Arcade.Sdk" Condition="'$(DISABLE_ARCADE)' != 'true'"/>
<Import Project="eng\targets\Settings.props" Condition="'$(DISABLE_ARCADE)' != 'true'"/>
```

**Line 12**: BUILD_IN_FSHARP_REPOSITORY marker
```xml
<BUILD_IN_FSHARP_REPOSITORY>true</BUILD_IN_FSHARP_REPOSITORY>
```

**Lines 20-80**: Standard properties (versions, names, paths, target frameworks)
- `FSCoreVersion`
- `FSharpSourcesRoot`
- `ProtoOutputPath` = `$(ArtifactsDir)\Bootstrap` ← **BOOTSTRAP OUTPUT PATH**
- `WarningsAsErrors`
- etc.

**Lines 82-110**: **THE SECRET REDIRECTION LOGIC** ⭐⭐⭐

```xml
<!-- Line 82-84: ProtoOutputPath definition -->
<ProtoOutputPath>$(ArtifactsDir)\Bootstrap</ProtoOutputPath>

<!-- Lines 106-109: REDIRECTION CONDITION & TARGETS -->
<PropertyGroup Condition="'$(Configuration)' != 'Proto' AND '$(DisableCompilerRedirection)'!='true' AND Exists('$(ProtoOutputPath)')">
    <FSharpTargetsPath>$(ProtoOutputPath)\fsc\Microsoft.FSharp.Targets</FSharpTargetsPath>
    <FSharpPropsShim>$(ProtoOutputPath)\fsc\Microsoft.FSharp.NetSdk.props</FSharpPropsShim>
    <FSharpTargetsShim>$(ProtoOutputPath)\fsc\Microsoft.FSharp.NetSdk.targets</FSharpTargetsShim>
    <FSharpOverridesTargetsShim>$(ProtoOutputPath)\fsc\Microsoft.FSharp.Overrides.NetSdk.targets</FSharpOverridesTargetsShim>
</PropertyGroup>
```

**Conditions breakdown**:
- `'$(Configuration)' != 'Proto'` → Does NOT apply in Proto phase
- `'$(DisableCompilerRedirection)'!='true'` → Can be overridden to disable
- `Exists('$(ProtoOutputPath)')` → Gates the hijacking

---

### Build Targets: FSharpBuild.Directory.Build.targets
**Path**: `FSharpBuild.Directory.Build.targets`  
**Lines**: ~190 lines

**Lines 1-4**: Arcade and custom imports
```xml
<Import Project="Sdk.targets" Sdk="Microsoft.DotNet.Arcade.Sdk" Condition="'$(DISABLE_ARCADE)' != 'true'"/>
<Import Project="eng\targets\Imports.targets" Condition="'$(DISABLE_ARCADE)' != 'true'"/>
<Import Project="FSharp.Profiles.props" />
```

**Lines 95-105**: Task override for FSharp.Build
```xml
<PropertyGroup Condition="'$(Configuration)' != 'Proto' AND '$(DisableCompilerRedirection)'!='true' AND Exists('$(ProtoOutputPath)')">
    <FSharpBuildAssemblyFileOverride>$(ProtoOutputPath)\fsc\FSharp.Build.dll</FSharpBuildAssemblyFileOverride>
</PropertyGroup>  
<UsingTask TaskName="FSharpEmbedResourceText" AssemblyFile="$(FSharpBuildAssemblyFileOverride)" Override="true" Condition="'$(Configuration)' != 'Proto' AND '$(DisableCompilerRedirection)'!='true' AND Exists('$(ProtoOutputPath)')" />
<UsingTask TaskName="FSharpEmbedResXSource" AssemblyFile="$(FSharpBuildAssemblyFileOverride)" Override="true" Condition="'$(Configuration)' != 'Proto' AND '$(DisableCompilerRedirection)'!='true' AND Exists('$(ProtoOutputPath)')" />
```

---

### Configuration Profiles: FSharp.Profiles.props
**Path**: `FSharp.Profiles.props`  
**Lines**: ~30 lines (small file)

**Lines 1-7**: Proto configuration flag
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Proto'">
    <DefineConstants>BUILDING_WITH_LKG;$(DefineConstants)</DefineConstants>
</PropertyGroup>
```
- Sets `BUILDING_WITH_LKG` define only when `Configuration == 'Proto'`

**Lines 9-15**: Warnings configuration
```xml
<PropertyGroup Condition="'$(MSBuildProjectExtension)' == '.fsproj' and $(TolerateUnusedBindings) != 'true'">
    <WarningsAsErrors>1182;$(WarningsAsErrors)</WarningsAsErrors>
    <WarnOn>1182;3879;$(WarnOn)</WarnOn>
</PropertyGroup>
```

**Lines 17-29**: Framework-specific settings
```xml
<Choose>
    <When Condition=" '$(TargetFrameworkIdentifier)' == '.NETFramework' ">
    </When>
    <Otherwise>
        <PropertyGroup>
            <DefineConstants>$(DefineConstants);NETSTANDARD</DefineConstants>
            ...
        </PropertyGroup>
    </Otherwise>
</Choose>
```

---

### Target Framework Configuration: eng/TargetFrameworks.props
**Path**: `eng/TargetFrameworks.props`  
**Lines**: ~20 lines (very concise)

**Lines 1-14**: Default TFM
```xml
<PropertyGroup>
    <!-- Default target framework for F# products -->
    <FSharpNetCoreProductTargetFramework Condition="'$(FSharpNetCoreProductTargetFramework)' == ''">net10.0</FSharpNetCoreProductTargetFramework>
    <!-- Derive major version -->
    <FSharpNetCoreProductMajorVersion>$([System.Text.RegularExpressions.Regex]::Replace('$(FSharpNetCoreProductTargetFramework)', '^net(\d+)\.0$', '$1'))</FSharpNetCoreProductMajorVersion>
</PropertyGroup>
```
- **Default**: `net10.0`
- **Derivation**: Strips `net` prefix and `.0` suffix → `10`

---

## Bootstrap Tool Projects

### Lexer Generator: buildtools/fslex/fslex.fsproj
**Path**: `buildtools/fslex/fslex.fsproj`  
**Lines**: ~45 lines

**Line 4**: **Disable implicit reference** ← CRITICAL FOR BOOTSTRAP
```xml
<DisableImplicitFSharpCoreReference>true</DisableImplicitFSharpCoreReference>
```

**Lines 21-26**: Self-contained dependencies
```xml
<Compile Include="Lexing.fsi" />
<Compile Include="Lexing.fs" />
<Compile Include="Parsing.fsi" />
<Compile Include="Parsing.fs" />
<Compile Include="Arg.fsi" />
<Compile Include="Arg.fs" />
```
- These are copies of basic primitives, NOT references to FSharp.Core

**Lines 28-35**: Conditional FSharp.Core reference
```xml
<ItemGroup Condition="'$(FSHARPCORE_USE_PACKAGE)' != 'true'">
    <ProjectReference Include="$(MSBuildThisFileDirectory)..\..\src\FSharp.Core\FSharp.Core.fsproj" />
</ItemGroup>

<ItemGroup Condition="'$(FSHARPCORE_USE_PACKAGE)' == 'true'">
    <PackageReference Include="FSharp.Core" Version="$(FSharpCoreShippedPackageVersionValue)" />
</ItemGroup>
```

### Parser Generator: buildtools/fsyacc/fsyacc.fsproj
**Path**: `buildtools/fsyacc/fsyacc.fsproj`  
**Lines**: ~45 lines (same pattern as fslex)

---

## Compiler Projects

### F# Compiler Executable: src/fsc/fscProject/fsc.fsproj
**Path**: `src/fsc/fscProject/fsc.fsproj`  
**Lines**: ~40 lines

**Lines 5-10**: Configuration-dependent target frameworks
```xml
<PropertyGroup Condition="'$(Configuration)' != 'Proto'">
    <TargetFrameworks Condition="'$(OS)' != 'Unix'">net472;$(FSharpNetCoreProductTargetFramework)</TargetFrameworks>
    <TargetFrameworks Condition="'$(OS)' == 'Unix' or '$(BUILDING_USING_DOTNET)' == 'true'">$(FSharpNetCoreProductTargetFramework)</TargetFrameworks>
</PropertyGroup>

<PropertyGroup Condition="'$(Configuration)' == 'Proto'">
    <TargetFramework>$(FSharpNetCoreProductTargetFramework)</TargetFramework>
```
- **Proto**: Only net10.0  
- **Release**: net472 (Windows) or net10.0 (Unix/using-dotnet)

**Lines 30-40**: Target file import
```xml
<Import Project="$([MSBuild]::GetPathOfFileAbove('fsc.targets', '$(MSBuildThisFileDirectory)../'))" />
```

### Targets File: src/fsc/fsc.targets
**Path**: `src/fsc/fsc.targets`  
**Lines**: ~50 lines

**Lines 1-20**: Project properties
```xml
<PropertyGroup>
    <OutputType>Exe</OutputType>
    <Configurations>Debug;Release;Proto</Configurations>
```
- Explicitly lists all 3 configurations

**Lines 25-40**: FSharp.Core reference logic
```xml
<ItemGroup Condition="'$(FSHARPCORE_USE_PACKAGE)' != 'true'">
    <ProjectReference Include="$(FSharpSourcesRoot)\FSharp.Core\FSharp.Core.fsproj" />
</ItemGroup>

<ItemGroup Condition="'$(FSHARPCORE_USE_PACKAGE)' == 'true'">
    <PackageReference Include="FSharp.Core" Version="$(FSharpCoreShippedPackageVersionValue)" />
</ItemGroup>
```

**Lines 42-50**: Compiler and build tool references
```xml
<ItemGroup>
    <ProjectReference Include="$(MSBuildThisFileDirectory)../FSharp.Build/FSharp.Build.fsproj" />
    <ProjectReference Include="$(MSBuildThisFileDirectory)../Compiler/FSharp.Compiler.Service.fsproj" />
</ItemGroup>
```

---

## Other Key Files

### FSharp Interactive: src/fsi/fsiProject/fsi.fsproj
**Path**: `src/fsi/fsiProject/fsi.fsproj`  
**Pattern**: Identical to fsc.fsproj  
**Key Line**: Line 1: `Sdk="Microsoft.NET.Sdk"` ← Uses SDK's F# compiler

### FSharp.Core Library: src/FSharp.Core/FSharp.Core.fsproj
**Path**: `src/FSharp.Core/FSharp.Core.fsproj`  
**Key Lines 7-8**: TFM configuration
```xml
<TargetFrameworks Condition="'$(Configuration)' == 'Proto'">netstandard2.0</TargetFrameworks>
<TargetFrameworks Condition="'$(Configuration)' != 'Proto'">netstandard2.0;netstandard2.1</TargetFrameworks>
```

### Compiler Service: src/Compiler/FSharp.Compiler.Service.fsproj
**Path**: `src/Compiler/FSharp.Compiler.Service.fsproj`  
**Key**: This is the main F# compiler library, compiled by Bootstrap/fsc.dll during Release build

### NuGet Configuration: NuGet.config
**Path**: `NuGet.config`  
**Use**: Default NuGet feeds (no special bootstrap package sources)

---

## Quick File Lookup Table

| What You Need | File | Lines | Key Property/Condition |
|:---|:---:|:---:|:---|
| Entry point | `build.sh` | 18 | Sources `eng/build.sh` |
| Orchestration | `eng/build.sh` | ~310-330 | `bootstrap_dir`, `proto.proj publish` |
| Bootstrap projects | `proto.proj` | 6-11 | Lists 5 projects |
| Redirection logic | `FSharpBuild.Directory.Build.props` | 106-109 | Condition + FSharpTargetsPath |
| Proto config | `FSharp.Profiles.props` | 5-7 | BUILDING_WITH_LKG define |
| TFM default | `eng/TargetFrameworks.props` | 14 | net10.0 |
| fsc compiler | `src/fsc/fscProject/fsc.fsproj` | 1 | Sdk="Microsoft.NET.Sdk" |
| fsi interactive | `src/fsi/fsiProject/fsi.fsproj` | 1 | Sdk="Microsoft.NET.Sdk" |
| fslex tool | `buildtools/fslex/fslex.fsproj` | 4 | DisableImplicitFSharpCoreReference |
| fsyacc tool | `buildtools/fsyacc/fsyacc.fsproj` | 4 | DisableImplicitFSharpCoreReference |
| FSharp.Core | `src/FSharp.Core/FSharp.Core.fsproj` | 7-8 | TFM conditions |
| Targets override | `FSharpBuild.Directory.Build.targets` | 95-105 | FSharpBuildAssemblyFileOverride |

---

## Search Patterns

### Find all Proto conditions:
```bash
grep -r "Configuration.*Proto" --include="*.props" --include="*.targets" --include="*.fsproj"
```

### Find all redirection setting:
```bash
grep -r "FSharpTargetsPath" --include="*.props" --include="*.targets"
```

### Find DisableCompilerRedirection:
```bash
grep -r "DisableCompilerRedirection" .
```

### Find BUILDING_WITH_LKG:
```bash
grep -r "BUILDING_WITH_LKG" --include="*.props" --include="*.fs" --include="*.fsi"
```

### Find DisableImplicitFSharpCoreReference:
```bash
grep -r "DisableImplicitFSharpCoreReference" --include="*.fsproj"
```

---

## Property Override Examples

### From Command Line
```bash
# Override target framework
./build.sh /p:FSharpNetCoreProductTargetFramework=net9.0

# Disable redirection
./build.sh /p:DisableCompilerRedirection=true

# Both
./build.sh /p:FSharpNetCoreProductTargetFramework=net9.0 /p:DisableCompilerRedirection=true
```

### From Environment Variable
```bash
# Set .NET SDK path
export DOTNET_INSTALL_DIR=/opt/dotnet

# .NET SDK will be used to compile .NET projects
export PATH=$DOTNET_INSTALL_DIR:$PATH
```

---

## Verification Checklist

Use this to verify the bootstrap is working correctly:

- [ ] `artifacts/Bootstrap/fslex/fslex.dll` exists
- [ ] `artifacts/Bootstrap/fsyacc/fsyacc.dll` exists
- [ ] `artifacts/Bootstrap/fsc/fsc.dll` exists
- [ ] `artifacts/Bootstrap/fsc/Microsoft.FSharp.Targets` exists (file, not directory)
- [ ] `artifacts/Bootstrap/fsc/FSharp.Build.dll` exists
- [ ] `artifacts/bin/fsc/Release/net10.0/fsc.dll` exists
- [ ] `artifacts/bin/FSharp.Compiler.Service/Release/netstandard2.0/FSharp.Compiler.Service.dll` exists
