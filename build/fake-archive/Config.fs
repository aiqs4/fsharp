module Config

open Fake.Core

/// Build configurations
type Configuration =
    | Debug
    | Release
    | Proto

/// Target frameworks supported
type TargetFramework =
    | Net10_0
    | Net9_0
    | Net8_0
    member this.AsString =
        match this with
        | Net10_0 -> "net10.0"
        | Net9_0 -> "net9.0"
        | Net8_0 -> "net8.0"

/// Which phase of the build to run
type BuildPhase =
    | BootstrapOnly
    | CleanRebuild
    | FullRelease

/// Options parsed from the command line
type BuildOptions = {
    Configuration: Configuration
    TargetFramework: TargetFramework
    Phase: BuildPhase
    Verbose: bool
    SkipTests: bool
    BinaryLog: bool
}

/// Default build options
let defaultOptions : BuildOptions = {
    Configuration = Release
    TargetFramework = Net10_0
    Phase = FullRelease
    Verbose = false
    SkipTests = false
    BinaryLog = false
}

/// Parse command-line arguments into BuildOptions
let parseArgs (argv: string[]) : Result<BuildOptions, string> =
    let args = argv |> Array.toList |> function | _ :: xs -> xs | [] -> []
    let rec loop opts remaining =
        match remaining with
        | [] -> Ok opts
        | "--configuration" :: value :: rest
        | "-c" :: value :: rest
        | "--cfg" :: value :: rest ->
            let config =
                match value.ToLower() with
                | "debug" -> Debug
                | "release" -> Release
                | "proto" -> Proto
                | _ -> opts.Configuration
            loop { opts with Configuration = config } rest
        | "--tfm" :: value :: rest ->
            let tfm =
                match value.ToLower() with
                | "net10.0" -> Net10_0
                | "net9.0" -> Net9_0
                | "net8.0" -> Net8_0
                | _ -> opts.TargetFramework
            loop { opts with TargetFramework = tfm } rest
        | "--bootstrap" :: rest ->
            loop { opts with Phase = CleanRebuild } rest
        | "--verbose" :: rest ->
            loop { opts with Verbose = true } rest
        | "--skip-tests" :: rest ->
            loop { opts with SkipTests = true } rest
        | "--binarylog" :: rest ->
            loop { opts with BinaryLog = true } rest
        | "--help" :: _ ->
            Error "Usage: fake build [--configuration <Debug|Release|Proto>] [--tfm <net8.0|net9.0|net10.0>] [--bootstrap] [--verbose] [--skip-tests] [--binarylog]"
        | unknown :: _ ->
            Error (sprintf "Unknown argument: %s" unknown)
    loop defaultOptions args
