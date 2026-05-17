module Paths

open System.IO

/// Root of the repository (two levels up from this file)
let root =
    __SOURCE_DIRECTORY__
    |> Path.GetDirectoryName
    |> Path.GetDirectoryName

/// Main artifact directories
let artifacts = Path.Combine(root, "artifacts")
let artifactsBin = Path.Combine(artifacts, "bin")
let artifactsBootstrap = Path.Combine(artifacts, "Bootstrap")
let artifactsLog = Path.Combine(artifacts, "log")

/// Bootstrap subfolders
let bootstrapFsc = Path.Combine(artifactsBootstrap, "fsc")
let bootstrapFslex = Path.Combine(artifactsBootstrap, "fslex")
let bootstrapFsyacc = Path.Combine(artifactsBootstrap, "fsyacc")

/// Key bootstrap files
let bootstrapFscDll = Path.Combine(bootstrapFsc, "fsc.dll")
let bootstrapFscRuntimeConfig = Path.Combine(bootstrapFsc, "fsc.runtimeconfig.json")
let bootstrapMicrosoftFSharpTargets = Path.Combine(bootstrapFsc, "Microsoft.FSharp.Targets")

/// Release compiler output paths
let releaseFscDll (configuration:string) (tfm:string) =
    Path.Combine(artifactsBin, "fsc", configuration, tfm, "fsc.dll")

let releaseCoreDll (configuration:string) (tfm:string) =
    Path.Combine(artifactsBin, "FSharp.Core", configuration, tfm, "FSharp.Core.dll")

/// Project files
let protoProj = Path.Combine(root, "proto.proj")
let fsharpSln = Path.Combine(root, "FSharp.sln")
let fscFsproj = Path.Combine(root, "src", "fsc", "fscProject", "fsc.fsproj")

/// Ensure a file or directory exists; fail early if not
let ensureExists path =
    if not (File.Exists path || Directory.Exists path) then
        failwithf "Expected path not found: %s" path
    path

/// Validate that bootstrap produced the required files
let validateBootstrap () =
    [ bootstrapFscDll
      bootstrapFscRuntimeConfig
      bootstrapMicrosoftFSharpTargets ]
    |> List.iter ensureExists