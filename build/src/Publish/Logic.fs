/// Publication targets (exe and nuget package creation)
/// Handles packing and distribution artifact generation

namespace FSharpBuild.Publish

open System
open System.Diagnostics
open System.IO
open FSharpBuild.Types

module Logic =
    
    /// Pack NuGet package for a project
    let packNuget 
        (ctx: BuildContext)
        (projectPath: string)
        (outputDir: string)
        : Result<TimeSpan, string> =
        
        try
            if not (File.Exists projectPath) then
                Error $"Project not found: {projectPath}"
            else
                let startTime = DateTime.Now
                
                let args = [
                    "pack"
                    projectPath
                    "-c"; ctx.Config.Configuration.ToString()
                    "--no-build"
                    "-o"; outputDir
                ]
                
                let psi = ProcessStartInfo(
                    FileName = "dotnet",
                    Arguments = String.concat " " args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                )
                
                use proc = Process.Start(psi)
                let _output = proc.StandardOutput.ReadToEnd()
                let errors = proc.StandardError.ReadToEnd()
                proc.WaitForExit()
                
                let duration = DateTime.Now - startTime
                
                if proc.ExitCode <> 0 then
                    Error $"Pack failed (exit {proc.ExitCode}): {errors}"
                else
                    Ok duration
        
        with ex ->
            Error $"Pack exception: {ex.Message}"
    
    /// Publish exe project
    let publishExe 
        (ctx: BuildContext)
        (projectPath: string)
        (outputDir: string)
        (runtime: string option)  // None for framework-dependent, Some("win-x64") for self-contained
        : Result<TimeSpan, string> =
        
        try
            if not (File.Exists projectPath) then
                Error $"Project not found: {projectPath}"
            else
                let startTime = DateTime.Now
                
                let args = 
                    [
                        "publish"
                        projectPath
                        "-c"; ctx.Config.Configuration.ToString()
                        "--no-build"
                        "-o"; outputDir
                    ] 
                    @ (match runtime with
                       | Some r -> ["-r"; r]
                       | None -> ["--no-self-contained"])
                
                let psi = ProcessStartInfo(
                    FileName = "dotnet",
                    Arguments = String.concat " " args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                )
                
                use proc = Process.Start(psi)
                let _output = proc.StandardOutput.ReadToEnd()
                let errors = proc.StandardError.ReadToEnd()
                proc.WaitForExit()
                
                let duration = DateTime.Now - startTime
                
                if proc.ExitCode <> 0 then
                    Error $"Publish failed (exit {proc.ExitCode}): {errors}"
                else
                    Ok duration
        
        with ex ->
            Error $"Publish exception: {ex.Message}"
    
    /// Package NuGet libraries
    let packageLibraries 
        (ctx: BuildContext)
        (outputDir: string)
        : Result<string list, string> =
        
        let projects = [
            "FSharp.Core"
            "FSharp.Compiler.Service"
        ]
        
        projects
        |> List.fold (fun acc projName ->
            match acc with
            | Error e -> Error e
            | Ok results ->
                let projPath = Path.Combine(ctx.RootPath, $"src/{projName}/{projName}.fsproj")
                match packNuget ctx projPath outputDir with
                | Ok _duration ->
                    Ok (projName :: results)
                | Error e ->
                    Error e
        ) (Ok [])
        |> Result.map List.rev
    
    /// Package executables
    let packageExes 
        (ctx: BuildContext)
        (outputDir: string)
        : Result<string list, string> =
        
        let projects = [
            ("fsc", None)
            ("fsi", None)
        ]
        
        projects
        |> List.fold (fun acc (projName, runtime) ->
            match acc with
            | Error e -> Error e
            | Ok results ->
                let projPath = Path.Combine(ctx.RootPath, $"src/{projName}/{projName}Project/{projName}.fsproj")
                match publishExe ctx projPath outputDir runtime with
                | Ok _duration ->
                    Ok (projName :: results)
                | Error e ->
                    Error e
        ) (Ok [])
        |> Result.map List.rev

module Status =
    
    /// Report what artifacts were created
    let reportArtifacts (nugets: string list) (exes: string list) : unit =
        if nugets.Length > 0 then
            printfn "✓ NuGet packages:"
            nugets |> List.iter (fun pkg -> printfn "  • %s" pkg)
        
        if exes.Length > 0 then
            printfn "✓ Executables:"
            exes |> List.iter (fun exe -> printfn "  • %s" exe)
