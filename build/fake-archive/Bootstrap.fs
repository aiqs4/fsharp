module Bootstrap

open Fake.Core
open Fake.DotNet
open Fake.IO.FileSystem
open Paths
open Config

/// Clean bootstrap artifacts directory
let clean () =
    Shell.rm_rf artifactsBootstrap
    Trace.log "Cleaned bootstrap artifacts"

/// Build the bootstrap (proto) artifacts
let build (opts: BuildOptions) =
    Trace.logf "Building bootstrap (Configuration=%A, TFM=%s)" 
        opts.Configuration opts.TargetFramework.AsString

    let args =
        [ protoProj
          "-c"; "Proto"
          "/p:DotNetBuild=false"
          "/p:DotNetBuildSourceOnly=false" ]
        @ (if opts.Verbose then [ "-v"; "diag" ] else [])
        @ (if opts.BinaryLog then [ "-bl" ] else [])

    let result =
        DotNet.exec (fun p -> { p with WorkingDirectory = root }) "publish" args

    if result.ExitCode <> 0 then
        failwithf "Proto build failed with exit code %d" result.ExitCode

/// Validate bootstrap output
let validate () =
    Trace.log "Validating bootstrap artifacts"
    validateBootstrap()
    Trace.log "✓ Bootstrap validation passed"

/// Run bootstrap phase based on BuildPhase
let run (opts: BuildOptions) =
    match opts.Phase with
    | BootstrapOnly ->
        clean()
        build opts
        validate()
    | CleanRebuild ->
        clean()
        build opts
        validate()
    | FullRelease ->
        if not (File.exists bootstrapFscDll) then
            Trace.log "Bootstrap missing; rebuilding..."
            clean()
            build opts
        else
            Trace.log "Using existing bootstrap"
        validate()