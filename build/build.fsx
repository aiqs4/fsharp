// Load FAKE directly from NuGet without paket
#r "nuget: Fake.Core, 6.1.0"
#r "nuget: Fake.DotNet, 6.1.0"
#r "nuget: Fake.IO.FileSystem, 6.1.0"

#I "src"
#load "Types.fs"
#load "Config.fs"
#load "Paths.fs"
#load "Bootstrap/Logic.fs"
#load "Bootstrap/Targets.fs"
#load "Compiler/ProjectDAG.fs"
#load "Compiler/Logic.fs"
#load "Compiler/Targets.fs"
#load "Test/Suites.fs"
#load "Test/Logic.fs"
#load "Test/Targets.fs"
#load "Publish/Logic.fs"
#load "Publish/Targets.fs"
#load "Orchestration.fs"

open Fake.Core
open Fake.IO

// Initialize and run the build
FSharpBuild.Bootstrap.Targets.initializeTargets()
FSharpBuild.Compiler.Targets.initializeTargets()
FSharpBuild.Test.Targets.initializeTargets()
FSharpBuild.Publish.Targets.initializeTargets()
FSharpBuild.Orchestration.Initialization.initializeAll()

// Run the build with specified target, or "build:full" by default
Target.runOrDefault "build:full"
