# F# Compiler Bootstrap: FAKE Build System Specification

**Status**: Proposal / Specification  
**Audience**: Maintainers, Core Contributors  
**Goal**: Replace bash+MSBuild bootstrap with pure-F# FAKE automation  
**Benefits**: Type safety, readability, maintainability, cross-platform reliability

---

## Current State: The Problem

### Existing Architecture
```
./build.sh (bash)
  └─ eng/build.sh (bash with MSBuild invocation)
     ├─ Check artifacts/Bootstrap exists (string ops, error-prone)
     ├─ dotnet publish proto.proj -c Proto (MSBuild XML logic)
     │  └─ FSharpBuild.Directory.Build.props (conditional XML)
     │     └─ FSharp.Profiles.props (more conditionals)
     └─ MSBuild FSharp.sln with redirected compiler paths
        └─ More FSharp.Build.props logic with string concatenation
```

### Pain Points

1. **No Type Safety**
   - Condition strings like `'$(Configuration)' != 'Proto'` are stringly-typed
   - Typos in property names fail silently at build time
   - No IDE support for XML conditions

2. **Scattered Logic**
   - Bootstrap logic split across: bash script, props files, targets files, proj files
   - Hard to understand complete flow without reading 5+ files
   - Maintenance burden when adding new platforms/TFMs

3. **Error Handling**
   - Bash scripts use exit codes; hard to debug intermediate failures
   - MSBuild errors buried in XML parsing
   - No retry logic for transient failures

4. **Properties as Configuration**
   - Properties like `FSharpTargetsPath` are defined conditionally in XML
   - Setting them requires coordinating multiple files
   - Easy to misconfigure; hard to validate before build starts

5. **Hardcoded Paths**
   - Paths like `artifacts/Bootstrap/fsc/` hardcoded in multiple places
   - Changing output structure requires hunting through 5+ files
   - No constants or named locations

---

## Proposed Solution: FAKE Build System

### Why FAKE?

- **Pure F#**: Same language as the codebase! Familiar to F# developers.
- **Type-Safe**: F# compiler catches configuration errors before build starts.
- **Composable**: Higher-order functions make build tasks reusable.
- **Cross-Platform**: Built-in abstractions over Windows/Linux/macOS differences.
- **Readable**: FSharp.Core + idioms are more readable than bash+XML.
- **Testable**: Build logic can unit-tested (e.g., path computation, version parsing).

### High-Level Design

```
build/Build.fs (entry point)
├── Modules/
│   ├── Paths.fs (type-safe path definitions)
│   ├── Config.fs (build configuration with type safety)
│   ├── Bootstrap.fs (Proto phase - bootstrap compiler)
│   ├── Compiler.fs (Release/Debug phase - self-compilation)
│   ├── Artifacts.fs (artifact collection and validation)
│   └── Targets.fs (public build targets)
└── build.sh (thin wrapper invoking FAKE)
```

---

## Module Design

### 1. **Paths.fs** - Centralized Path Management

```fsharp
module Paths =
  open System.IO
  
  let root = __SOURCE_DIRECTORY__ |> Path.GetDirectoryName |> Path.GetDirectoryName
  
  let artifacts = Path.Combine(root, "artifacts")
  let artifactsBin = Path.Combine(artifacts, "bin")
  let artifactsBootstrap = Path.Combine(artifacts, "Bootstrap")
  let artifactsLog = Path.Combine(artifacts, "log")
  
  // Compiler output locations
  let bootstrapFsc = Path.Combine(artifactsBootstrap, "fsc")
  let bootstrapFslex = Path.Combine(artifactsBootstrap, "fslex")
  let bootstrapFsyacc = Path.Combine(artifactsBootstrap, "fsyacc")
  
  // Key bootstrap files
  let bootstrapFscDll = Path.Combine(bootstrapFsc, "fsc.dll")
  let bootstrapFscRuntimeConfig = Path.Combine(bootstrapFsc, "fsc.runtimeconfig.json")
  let bootstrapMicrosoftFSharpTargets = Path.Combine(bootstrapFsc, "Microsoft.FSharp.Targets")
  
  // Release compiler output
  let releaseFscDll config tfm = 
    Path.Combine(artifactsBin, "fsc", "Release", tfm, "fsc.dll")
  let releaseCoreDll config tfm =
    Path.Combine(artifactsBin, "FSharp.Core", "Release", tfm, "FSharp.Core.dll")
  
  // Project files
  let protoProj = Path.Combine(root, "proto.proj")
  let fsharpSln = Path.Combine(root, "FSharp.sln")
  let fscFscProj = Path.Combine(root, "src", "fsc", "fscProject", "fsc.fsproj")
  
  /// Validate that a path exists; raise if not
  let ensureExists path =
    if not (File.Exists path || Directory.Exists path) then
      failwithf "Expected path not found: %s" path
    path
  
  /// Validate bootstrap is complete
  let validateBootstrap () =
    [ bootstrapFscDll
      bootstrapFscRuntimeConfig
      bootstrapMicrosoftFSharpTargets ]
    |> List.iter ensureExists
```

**Benefits**:
- Single source of truth for all paths
- `ensureExists` catches missing files early
- Type-safe references (no string concatenation)
- Easy to refactor output structure

---

### 2. **Config.fs** - Build Configuration

```fsharp
module Config =
  type Configuration = Debug | Release | Proto
  
  type TargetFramework = 
    | Net10_0 | Net9_0 | Net8_0
    member this.AsString = 
      match this with
      | Net10_0 -> "net10.0"
      | Net9_0 -> "net9.0"
      | Net8_0 -> "net8.0"
  
  type BuildPhase = 
    | BootstrapOnly
    | FullRelease
    | CleanRebuild
  
  type BuildOptions = {
    Configuration: Configuration
    TargetFramework: TargetFramework
    Phase: BuildPhase
    Verbose: bool
    SkipTests: bool
  }
  
  /// Parse command-line arguments into BuildOptions
  let parseArgs argv : Result<BuildOptions, string> =
    // Type-safe argument parsing with validation
    // Returns Result to allow error handling
    Ok { 
      Configuration = Release
      TargetFramework = Net10_0
      Phase = FullRelease
      Verbose = false
      SkipTests = false
    }
  
  let defaultOptions = {
    Configuration = Release
    TargetFramework = Net10_0
    Phase = FullRelease
    Verbose = false
    SkipTests = false
  }
```

**Benefits**:
- Discriminated unions prevent invalid states (e.g., can't accidentally pass wrong config)
- `Result<'T, 'E>` for error handling with proper types
- Centralized validation logic
- Can be tested independently

---

### 3. **Bootstrap.fs** - Proto Phase

```fsharp
module Bootstrap =
  open Fake.Core
  open Fake.DotNet
  
  let clean () =
    Shell.rm_rf Paths.artifactsBootstrap
    Trace.log "Cleaned bootstrap artifacts"
  
  let build opts =
    Trace.logf "Building bootstrap (Configuration=%A, TFM=%s)" 
      opts.Configuration 
      opts.TargetFramework.AsString
    
    let args = [
      "publish"
      Paths.protoProj
      "-c"; "Proto"
      if opts.Verbose then "-v"; "diag"
    ]
    
    let result = DotNet.exec (fun p ->
      { p with WorkingDirectory = Paths.root }
    ) "" args
    
    if result.ExitCode <> 0 then
      failwithf "Proto build failed with exit code %d" result.ExitCode
  
  let validate () =
    Trace.log "Validating bootstrap artifacts..."
    Paths.validateBootstrap ()
    Trace.log "✓ Bootstrap validation passed"
  
  let run opts =
    match opts.Phase with
    | Config.BootstrapOnly ->
        clean ()
        build opts
        validate ()
    | Config.CleanRebuild ->
        clean ()
        build opts
        validate ()
    | Config.FullRelease ->
        if not (File.exists Paths.bootstrapFscDll) then
          Trace.log "Bootstrap missing; rebuilding..."
          clean ()
          build opts
        else
          Trace.log "Using existing bootstrap"
        validate ()
```

**Benefits**:
- Each step (clean, build, validate) is a separate function → reusable
- `Fake.Core` handles shell execution properly
- Clear error messages with context
- Can compose with other phases

---

### 4. **Compiler.fs** - Release Phase

```fsharp
module Compiler =
  open Fake.Core
  open Fake.DotNet
  
  let build opts =
    Trace.logf "Building F# compiler (Configuration=%A, TFM=%s)" 
      opts.Configuration 
      opts.TargetFramework.AsString
    
    // Validate bootstrap first
    Bootstrap.validate ()
    
    let args = [
      "build"
      Paths.fsharpSln
      "-c"; (match opts.Configuration with
             | Config.Release -> "Release"
             | Config.Debug -> "Debug"
             | Config.Proto -> failwith "Cannot build Release phase in Proto mode")
      "-p:TFM=" + opts.TargetFramework.AsString
      if opts.Verbose then "-v"; "diag"
    ]
    
    let result = DotNet.exec (fun p ->
      { p with 
          WorkingDirectory = Paths.root
          // Use bootstrap compiler
          Environment = [
            "FSHARP_TARGETS_PATH", Paths.bootstrapMicrosoftFSharpTargets
          ] |> Map.ofList
      }
    ) "" args
    
    if result.ExitCode <> 0 then
      failwithf "Compiler build failed with exit code %d" result.ExitCode
  
  let verify opts =
    Trace.log "Verifying compiled artifacts..."
    let tfm = opts.TargetFramework.AsString
    let fscDll = Paths.releaseFscDll opts.Configuration tfm
    let coreDll = Paths.releaseCoreDll opts.Configuration tfm
    
    [fscDll; coreDll] |> List.iter Paths.ensureExists
    Trace.log "✓ Self-compilation successful (F# compiled itself!)"
```

**Benefits**:
- Separation from Bootstrap phase
- Can test compiler build independently
- Clear validation after build
- Environment setup is explicit

---

### 5. **Targets.fs** - Public Build Interface

```fsharp
module Targets =
  open Fake.Core
  
  let initTargets (opts: Config.BuildOptions) =
    Target.create "Clean" (fun _ ->
      Bootstrap.clean ()
      Trace.log "Clean complete"
    )
    
    Target.create "BuildBootstrap" (fun _ ->
      Bootstrap.run { opts with Phase = Config.BootstrapOnly }
    )
    
    Target.create "BuildCompiler" (fun _ ->
      Compiler.build opts
    )
    
    Target.create "Verify" (fun _ ->
      Compiler.verify opts
    )
    
    Target.create "Build" (fun _ ->
      Trace.log "=== F# Compiler Build ==="
      Bootstrap.run opts
      Compiler.build opts
      Compiler.verify opts
    )
    
    Target.create "Rebuild" (fun _ ->
      Bootstrap.clean ()
      Target.run "Build" []
    )
    
    // Build target dependencies
    "BuildCompiler" |> Target.dependsOn "BuildBootstrap"
    "Verify" |> Target.dependsOn "BuildCompiler"
    "Build" |> Target.dependsOn "Verify"
```

---

### 6. **Build.fs** - Entry Point

```fsharp
open Fake.Core

module Main =
  [<EntryPoint>]
  let main argv =
    try
      let opts = 
        Config.parseArgs argv
        |> function
           | Ok o -> o
           | Error msg -> 
               Trace.error msg
               exit 1
      
      Trace.logf "Building with: %A" opts
      
      Targets.initTargets opts
      
      Target.runOrDefault "Build"
      
      0
    with
    | Failure msg ->
        Trace.error msg
        1
    | ex ->
        Trace.error (sprintf "Unexpected error: %s" ex.Message)
        1
```

---

## Building with FAKE

### File Structure
```
build/
├── Build.fs (entry point, ~50 lines)
├── Targets.fs (target definitions, ~40 lines)
├── Bootstrap.fs (bootstrap logic, ~50 lines)
├── Compiler.fs (compiler build logic, ~50 lines)
├── Config.fs (configuration, ~60 lines)
├── Paths.fs (path definitions, ~50 lines)
├── build.fsx (FAKE entrypoint script, ~10 lines)
└── paket.lock (FAKE dependency)
```

### Usage

```bash
# Build with defaults (Release, net10.0, FullRelease)
fake build

# Bootstrap only (no self-compilation)
fake build --target BuildBootstrap

# Clean rebuild
fake build --target Rebuild

# Verbose output
fake build -- --verbose

# Custom target framework
fake build -- --tfm net9.0

# Debug mode
fake build -- --cfg Debug
```

### Thin Wrapper: build.sh

```bash
#!/bin/bash
set -e

cd "$(dirname "$0")"

# Simple wrapper that delegates to FAKE
if command -v fake &> /dev/null; then
  fake build "$@"
else
  echo "FAKE not found. Install via: dotnet tool install -g fake-cli"
  exit 1
fi
```

---

## Comparison: Before vs. After

### Bootstrap Validation

**Before (bash)**:
```bash
# eng/build.sh, lines 310-330
bootstrap_dir=$artifacts_dir/Bootstrap
if [ ! -f "$bootstrap_dir/fslex/fslex.dll" ]; then
    # Bootstrap needed - but how do we know if it's complete?
    # Must check multiple files individually
fi
```

**After (FAKE)**:
```fsharp
// Bootstrap.fs
let validate () =
  [ bootstrapFscDll
    bootstrapFscRuntimeConfig
    bootstrapMicrosoftFSharpTargets ]
  |> List.iter Paths.ensureExists
```

### Compiler Invocation

**Before (bash)**:
```bash
# eng/build.sh, lots of lines
MSBuild $toolset_build_proj \
  /p:Configuration=$configuration \
  /p:Projects="$projects" \
  /p:FSharpTargetsPath=$bootstrap_dir \    # String concatenation!
  # ... 10+ more /p: arguments
```

**After (FAKE)**:
```fsharp
// Compiler.fs
Environment = [
  "FSHARP_TARGETS_PATH", Paths.bootstrapMicrosoftFSharpTargets
] |> Map.ofList
```

### Error Handling

**Before**:
```bash
# Shell exit codes; hard to debug
dotnet publish proto.proj -c Proto || exit 1
```

**After**:
```fsharp
let result = DotNet.exec ...
if result.ExitCode <> 0 then
  failwithf "Proto build failed with exit code %d" result.ExitCode
```

---

## Implementation Path

### Phase 1: Create Parallel FAKE Build
1. Create `build/` directory with FAKE skeleton
2. Port bootstrap logic to `Bootstrap.fs`
3. Port compiler logic to `Compiler.fs`
4. Test on CI: `fake build` produces same artifacts as `./build.sh -c Release`

### Phase 2: Transition
1. Update documentation to mention FAKE
2. `./build.sh` now invokes `fake build` internally
3. Both paths work during transition period

### Phase 3: Deprecation
1. Remove bash logic from `eng/build.sh`
2. Delete conditional props/targets complexity from MSBuild files
3. `build.sh` is now just thin wrapper; all logic in FAKE

---

## Alternative: Simpler Bake Build System

If FAKE is too heavy, a minimal pure-F# build alternative:

```fsharp
// build.fsx - single file, ~200 LOC
#load "paket-files/github.com/fsprojects/Fake/modules/Fake/FakeLib.fsx"

let root = __SOURCE_DIRECTORY__
let artifacts = root </> "artifacts"
let bootstrap = artifacts </> "Bootstrap"

// Clean
Shell.cleanDirs [bootstrap]

// Bootstrap
let exitCode = Shell.Exec(sprintf "dotnet publish proto.proj -c Proto")
if exitCode <> 0 then exit exitCode

// Verify
["fsc/fsc.dll"; "fsc/Microsoft.FSharp.Targets"; "fslex/fslex.dll"]
|> List.iter (fun p -> 
    if not (File.exists (bootstrap </> p)) then
      failwithf "Missing: %s" p
)

// Compile
let compiler = bootstrap </> "fsc" </> "Microsoft.FSharp.Targets"
Shell.Exec(sprintf "dotnet msbuild FSharp.sln /p:Configuration=Release /p:FSharpTargetsPath=%s" compiler) |> ignore
```

**Pros**:
- Single file, easy to maintain
- No module system overhead
- Can inline small functions

**Cons**:
- No testability
- Gets messy as complexity grows
- String-based operations creep back in

**Recommendation**: Start with Bake/FSI script; when >500 LOC, graduate to full FAKE project.

---

## Validation Checklist

When implementing FAKE build:

- [ ] `fake build` produces identical artifacts to `./build.sh -c Release`
- [ ] `fake build --target Clean` removes artifacts correctly
- [ ] Bootstrap validation catches missing `runtimeconfig.json`
- [ ] Error messages are clear (include file paths, exit codes)
- [ ] Works on Windows, Linux, macOS
- [ ] `fake build --help` lists all targets
- [ ] `.gitlab-ci.yml` / `.github/workflows` use `fake build` instead of bash
- [ ] Documentation updated with new build commands

---

## Benefits Summary

| Aspect | Bash+MSBuild | FAKE |
|--------|-----|------|
| Language | bash + XML | F# |
| Type Safety | None | Full (compiler checks) |
| IDE Support | None | IntelliSense in VS Code |
| Error Messages | Cryptic MSBuild XML | Readable F# exceptions |
| Code Reuse | Hard (sed/awk) | Natural (functions) |
| Testing | Manual | Can unit test |
| Maintainability | Hard (5+ files) | Easy (1-2 files) |
| Cross-Platform | Fragile | Robust (Fake.Core abstractions) |
| Performance | Fast | Fast |
| Learning Curve | Already known | F# skill required |

---

## References

- **FAKE Documentation**: https://fake.build/
- **FAKE Best Practices**: https://fake.build/guide/getting-started.html
- **F# Project Structure**: Project Scaffold patterns
- Original bootstrap docs: BOOTSTRAP_GUIDE.md, BOOTSTRAP_INSIGHTS.md

---

## Questions & Discussion

1. **Should we keep MSBuild for CI/CD?** 
   - Yes, but FAKE handles orchestration; MSBuild is just a task.

2. **What about `./build.sh` shell scripts in subfolders?**
   - FAKE can replace all of them; consolidate into `build/Build.fs`.

3. **Support for building just FSharp.Core?**
   - Add `fake build --target BuildCore` target.

4. **How to handle matrix builds (multiple frameworks)?**
   - Add loop in Build.fs; generate multiple artifacts.

5. **Is FAKE stable?**
   - Yes, mature ecosystem (used in F# Foundation projects, dotnet/fsharp).
