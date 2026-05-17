/// Compiler build orchestration
/// Handles the actual dotnet build commands, environment setup, and result tracking

namespace FSharpBuild.Compiler

open System
open System.Diagnostics
open System.IO
open FSharpBuild.Types
open FSharpBuild.Paths
open FSharpBuild.Bootstrap

module Logic =
    
    /// Build a single project with dotnet
    /// Returns: success, duration, output lines, error count, warning count
    let buildProject 
        (ctx: BuildContext) 
        (project: ProjectInfo) 
        (bootstrapEnv: (string * string) list)
        : Result<BuildResult, string> =
        
        try
            let projectPath = project.FullPath
            if not (File.Exists projectPath) then
                Error $"Project not found: {projectPath}"
            else
                let startTime = DateTime.Now
                
                // Build arguments
                let args = [
                    "build"
                    projectPath
                    "-c"; project.Layer.ToString()
                    "--no-restore"
                    "-p:TreatWarningsAsErrors=false"
                ]
                
                let psi = ProcessStartInfo(
                    FileName = "dotnet",
                    Arguments = String.concat " " args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                )
                
                // Add bootstrap environment if provided
                bootstrapEnv
                |> List.iter (fun (key, value) ->
                    psi.EnvironmentVariables[key] <- value
                )
                
                use proc = Process.Start(psi)
                let output = proc.StandardOutput.ReadToEnd()
                let errors = proc.StandardError.ReadToEnd()
                proc.WaitForExit()
                
                let duration = DateTime.Now - startTime
                let outputLines = output.Split('\n') |> Array.toList
                
                let success = proc.ExitCode = 0
                let errorCount = if success then 0 else 1
                let warningCount = 
                    (output + errors).Split('\n') 
                    |> Array.countWhere (fun line -> line.Contains("warning"))
                
                Ok {
                    Target = {
                        Phase = BuildPhase.Compiler
                        Component = None
                        Parallel = None
                    }
                    Success = success
                    Duration = duration
                    OutputLines = outputLines
                    ErrorCount = errorCount
                    WarningCount = warningCount
                }
        
        with ex ->
            Error $"Build exception: {ex.Message}"
    
    /// Build multiple projects in calculated order
    let buildProjects 
        (ctx: BuildContext)
        (projects: ProjectInfo list)
        (parallel: int)
        : BuildResult list =
        
        let buildEnv = Bootstrap.Logic.getBootstrapEnv ctx
        
        if parallel = 1 then
            // Sequential build
            projects
            |> List.map (fun proj ->
                match buildProject ctx proj buildEnv with
                | Ok result -> result
                | Error e ->
                    {
                        Target = {
                            Phase = BuildPhase.Compiler
                            Component = None
                            Parallel = None
                        }
                        Success = false
                        Duration = TimeSpan.Zero
                        OutputLines = [e]
                        ErrorCount = 1
                        WarningCount = 0
                    }
            )
        else
            // Parallel build (with limited parallelism)
            projects
            |> List.Parallel.map (fun proj ->
                match buildProject ctx proj buildEnv with
                | Ok result -> result
                | Error e ->
                    {
                        Target = {
                            Phase = BuildPhase.Compiler
                            Component = None
                            Parallel = None
                        }
                        Success = false
                        Duration = TimeSpan.Zero
                        OutputLines = [e]
                        ErrorCount = 1
                        WarningCount = 0
                    }
            )
            |> Array.toList
    
    /// Build specific layer
    let buildLayer 
        (ctx: BuildContext)
        (layer: BuildLayer)
        (dag: ProjectDAG.DepGraph)
        (parallel: int)
        : BuildResult list =
        
        let projects = ProjectDAG.getLayer dag layer
        buildProjects ctx projects parallel
    
    /// Build all in compilation order
    let buildAll 
        (ctx: BuildContext)
        (dag: ProjectDAG.DepGraph)
        (parallel: int)
        : Result<BuildResult list, string> =
        
        match ProjectDAG.computeBuildOrder dag with
        | Ok ordered ->
            let results = buildProjects ctx ordered parallel
            Ok results
        | Error e -> Error e

module Status =
    
    /// Report compilation status
    let reportResults (results: BuildResult list) : string =
        let successful = results |> List.countWhere (fun r -> r.Success)
        let failed = results |> List.countWhere (fun r -> not r.Success)
        let totalTime = 
            results 
            |> List.map (fun r -> r.Duration.TotalSeconds) 
            |> List.sum
        
        sprintf 
            "Compilation: %d/% d passed in %.1fs (failures: %d)"
            successful results.Length totalTime failed
    
    /// Detailed build report
    let detailReport (results: BuildResult list) : string list =
        results
        |> List.map (fun r -> r.Summary)
