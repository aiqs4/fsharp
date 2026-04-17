**Yes — you are right to ask.**  

**Paket is outdated for new FAKE projects in 2026.**

### Current best practice (FAKE 6+)

The FAKE team and the broader F# community have moved away from Paket for simple-to-medium build scripts.  
The modern, officially recommended way is the **“paket-less” style** using direct `#r "nuget: …"` directives inside `build.fsx`.

**Advantages of the modern style:**
- No `paket.dependencies`, no `paket.lock`, no `paket-files/` folder
- No extra `dotnet tool restore` or `paket install` step
- Faster startup
- Fewer files to maintain
- Still fully reproducible (FAKE caches the packages)
- Matches what new FAKE templates and most open-source F# projects now use

### Updated recommendation for your F# compiler bootstrap

Replace the Paket-based setup I gave you earlier with this **cleaner, modern structure**:

```text
build/
├── Build.fs
├── Targets.fs
├── Bootstrap.fs
├── Compiler.fs
├── Config.fs
├── Paths.fs
├── Artifacts.fs
├── build.fsx                 ← only this file changes significantly
└── build.sh                  ← unchanged (thin wrapper)
```

### New `build/build.fsx` (modern style)

```fsharp
#r "nuget: Fake.Core.Target, 6.*"
#r "nuget: Fake.DotNet.Cli, 6.*"
#r "nuget: Fake.IO.FileSystem, 6.*"
#r "nuget: Fake.Core.Environment, 6.*"
#r "nuget: Fake.Core.Process, 6.*"
#r "nuget: Fake.Core.Trace, 6.*"

#load "Build.fs"

open Fake.Core
open Fake.Core.TargetOperators

// This is the only thing needed to start the build
Build.Main.main (System.Environment.GetCommandLineArgs())
```

That’s it. No `paket.dependencies`, no `paket.lock`.

### What you should do now

1. **Delete** any `paket.*` files I previously told you to create.
2. Create the `build.fsx` exactly as above.
3. Keep all the other `.fs` modules **exactly** as I described before (Paths, Config, Bootstrap, etc.).
4. Make the `build.sh` wrapper point to `fake build` (unchanged).

### When would you still use Paket?

Only in these rare cases:
- You have a very large/complex build with dozens of dependencies
- You need very strict lock-file control across many machines
- You are inside the official `dotnet/fsharp` repo and want to stay 100% consistent with their existing Paket usage

For this bootstrap replacement, the modern `#r "nuget:"` style is clearly better.

Would you like me to give you the **complete updated instructions** (replacing the previous ones) with the paket-less `build.fsx` and any tiny adjustments to the other modules? Just say the word and I’ll rewrite the full “Anweisungen für den AI Coder” with the modern setup.