/// Configuration parsing and environment setup
/// Handles CLI arguments, environment variables, and default configuration

namespace FSharpBuild.Config

open System
open System.IO
open FSharpBuild.Types

module Config =
    
    /// Parse configuration from CLI arguments
    let parseConfig (args: string list) : BuildConfig option =
        let mutable config = None
        let mutable platform = Platform.AnyCPU
        let mutable tf = None
        
        let rec parse (remaining: string list) =
            match remaining with
            | [] -> ()
            | "-c" :: cfg :: rest | "--configuration" :: cfg :: rest ->
                let c = 
                    match cfg.ToLower() with
                    | "debug" -> Configuration.Debug
                    | "release" -> Configuration.Release
                    | "proto" -> Configuration.Proto
                    | invalid -> failwith $"Invalid configuration: {invalid}"
                config <- Some c
                parse rest
            
            | "-p" :: plat :: rest | "--platform" :: plat :: rest ->
                platform <- 
                    match plat.ToLower() with
                    | "anycpu" | "any cpu" -> Platform.AnyCPU
                    | "x86" -> Platform.X86
                    | invalid -> failwith $"Invalid platform: {invalid}"
                parse rest
            
            | "-tf" :: tfstr :: rest | "--target-framework" :: tfstr :: rest ->
                tf <- 
                    Some (
                        match tfstr.ToLower() with
                        | "net472" -> TargetFramework.Net472
                        | "netstandard2.0" -> TargetFramework.NetStandard20
                        | "netstandard2.1" -> TargetFramework.NetStandard21
                        | "net10" | "net10.0" -> TargetFramework.Net10
                        | invalid -> failwith $"Invalid target framework: {invalid}"
                    )
                parse rest
            
            | arg :: rest ->
                if arg.StartsWith "-" then
                    printfn "Warning: Unknown argument: %s" arg
                parse rest
        
        parse args
        
        match config with
        | Some c -> 
            Some {
                Configuration = c
                Platform = platform
                TargetFramework = tf
            }
        | None -> None
    
    /// Get configuration from environment variable
    let fromEnvironment () : BuildConfig option =
        Environment.GetEnvironmentVariable("FSHARP_BUILD_CONFIG")
        |> Option.ofObj
        |> Option.bind (fun cfg ->
            match cfg.ToLower() with
            | "debug" -> Some { Configuration = Debug; Platform = AnyCPU; TargetFramework = None }
            | "release" -> Some { Configuration = Release; Platform = AnyCPU; TargetFramework = None }
            | "proto" -> Some { Configuration = Proto; Platform = AnyCPU; TargetFramework = None }
            | _ -> None
        )
    
    /// Default configuration
    let getDefault () : BuildConfig =
        {
            Configuration = Configuration.Debug
            Platform = Platform.AnyCPU
            TargetFramework = None
        }
    
    /// Resolve configuration: CLI > env > default
    let resolve (args: string list) : BuildConfig =
        match parseConfig args with
        | Some c -> c
        | None ->
            match fromEnvironment () with
            | Some c -> c
            | None -> getDefault ()
    
    /// Validate configuration against project constraints
    let validate (config: BuildConfig) (project: ProjectInfo) : Result<unit, string> =
        match config.TargetFramework with
        | None -> Ok ()
        | Some tf ->
            if List.contains tf project.TargetFrameworks then
                Ok ()
            else
                Error $"Project {project.Name} does not support {tf}"

module Environment =
    
    /// Check if running in CI
    let isCI () : bool =
        let ciEnvVars = ["GITHUB_ACTIONS"; "AZURE_PIPELINES"; "CI"; "BUILD_ID"]
        ciEnvVars 
        |> List.exists (fun var -> 
            System.Environment.GetEnvironmentVariable(var) |> Option.ofObj |> Option.isSome
        )
    
    /// Get parallelism
    let getParallelism () : int =
        let envParallel = System.Environment.GetEnvironmentVariable("FSHARP_BUILD_PARALLEL")
        match envParallel with
        | null -> System.Environment.ProcessorCount
        | str when Int32.TryParse str |> fst -> Int32.Parse str
        | _ -> System.Environment.ProcessorCount
    
    /// Check if verbose
    let isVerbose () : bool =
        System.Environment.GetEnvironmentVariable("FSHARP_BUILD_VERBOSE")
        |> Option.ofObj
        |> Option.map (fun s -> s.ToLower() = "true")
        |> Option.defaultValue false
    
    /// Check if strict
    let isStrict () : bool =
        System.Environment.GetEnvironmentVariable("FSHARP_BUILD_STRICT")
        |> Option.ofObj
        |> Option.map (fun s -> s.ToLower() = "true")
        |> Option.defaultValue false
