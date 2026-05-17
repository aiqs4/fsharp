module Compiler

open Fake.Core
open Fake.DotNet
open Paths
open Config
open Bootstrap

/// Build the F# compiler using the bootstrap compiler
let build (opts: BuildOptions) =
    Trace.logf "Building F# compiler (Configuration=%A, TFM=%s)" opts.Configuration opts.TargetFramework.AsString
    // Validate bootstrap artifacts
    Bootstrap.validate ()
    let configStr =
        match opts.Configuration with
        | Debug -> "Debug"
        | Release -> "Release"
        | Proto -> failwith "Cannot build compiler in Proto mode"

    let args =
        [ fsharpSln
          "-c"; configStr
          "--framework"; opts.TargetFramework.AsString ]
        @ (if opts.Verbose then [ "--verbosity"; "diagnostic" ] else [])
        @ (if opts.BinaryLog then [ "-bl" ] else [])

    let result =
        DotNet.exec (fun p ->
            { p with WorkingDirectory = root
                     Environment = [ "FSHARP_TARGETS_PATH", bootstrapMicrosoftFSharpTargets ] |> Map.ofList }
        ) "build" args

    if result.ExitCode <> 0 then
        failwithf "Compiler build failed with exit code %d" result.ExitCode

/// Verify that the self-compiled artifacts exist
let verify (opts: BuildOptions) =
    Trace.log "Verifying compiled artifacts..."
    let tfm = opts.TargetFramework.AsString
    let configStr =
        match opts.Configuration with
        | Configuration.Debug -> "Debug"
        | Configuration.Release -> "Release"
        | Configuration.Proto -> failwith "Cannot verify in Proto mode"

    [ Paths.releaseFscDll configStr tfm
      Paths.releaseCoreDll configStr tfm ]
    |> List.iter Paths.ensureExists
    Trace.log "✓ Self-compilation successful"