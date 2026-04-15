module Artifacts

open Fake.Core
open Paths
open Config

/// Collect and validate release artifacts
let collectArtifacts (opts: BuildOptions) =
    Trace.log "Collecting artifacts..."
    // TODO: gather outputs into a package directory or list

/// Validate release artifacts exist
let validateReleaseArtifacts (opts: BuildOptions) =
    Trace.log "Validating release artifacts..."
    // Example required files; expand as needed
    let tfm = opts.TargetFramework.AsString
    let configStr =
        match opts.Configuration with
        | Configuration.Debug -> "Debug"
        | Configuration.Release -> "Release"
        | Configuration.Proto -> failwith "Cannot validate release artifacts in Proto mode"
    [ Paths.releaseFscDll configStr tfm
      Paths.releaseCoreDll configStr tfm ]
    |> List.iter Paths.ensureExists
    Trace.log "✓ Release artifact validation passed"

/// Clean compiled bin artifacts
let cleanBin () =
    Trace.log "Cleaning bin artifacts..."
    Shell.rm_rf Paths.artifactsBin

/// (Optional) Pack NuGet packages
let packNuGets (opts: BuildOptions) =
    Trace.log "Packing NuGet packages..."
    // TODO: implement packing if needed