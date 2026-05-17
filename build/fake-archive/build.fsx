#r "nuget: Fake.Core.Target, 6.*, source=https://api.nuget.org/v3/index.json"
#r "nuget: Fake.DotNet.Cli, 6.*, source=https://api.nuget.org/v3/index.json"
#r "nuget: Fake.IO.FileSystem, 6.*, source=https://api.nuget.org/v3/index.json"
#r "nuget: Fake.Core.Environment, 6.*, source=https://api.nuget.org/v3/index.json"
#r "nuget: Fake.Core.Process, 6.*, source=https://api.nuget.org/v3/index.json"
#r "nuget: Fake.Core.Trace, 6.*, source=https://api.nuget.org/v3/index.json"

#load "Build.fs"

open Fake.Core
open Fake.Core.TargetOperators

// Entrypoint to the FAKE build
Build.Main.main (System.Environment.GetCommandLineArgs())