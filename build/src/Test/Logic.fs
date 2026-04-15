/// Test suite build and execution logic
/// Handles building test projects and running xUnit tests

namespace FSharpBuild.Test

open System
open System.Diagnostics
open System.IO
open FSharpBuild.Types
open FSharpBuild.Paths

module Logic =
    
    /// Build a test suite
    let buildSuite 
        (ctx: BuildContext)
        (suite: Suites.TestSuite)
        : Result<TimeSpan, string> =
        
        try
            let projectPath = Path.Combine(ctx.RootPath, suite.ProjectPath)
            
            if not (File.Exists projectPath) then
                Error $"Test project not found: {projectPath}"
            else
                let startTime = DateTime.Now
                
                let args = [
                    "build"
                    projectPath
                    "-c"; ctx.Config.Configuration.ToString()
                    "--no-restore"
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
                    Error $"Build failed (exit {proc.ExitCode}): {errors}"
                else
                    Ok duration
        
        with ex ->
            Error $"Build exception: {ex.Message}"
    
    /// Build all test suites
    let buildAll 
        (ctx: BuildContext)
        (suites: Suites.TestSuite list)
        (parallel: int)
        : Result<Suites.TestSuite list, string> =
        
        let buildOne (suite: Suites.TestSuite) : Result<Suites.TestSuite, string> =
            match buildSuite ctx suite with
            | Ok _duration -> Ok suite
            | Error e -> Error $"{suite.Name}: {e}"
        
        if parallel = 1 then
            // Sequential build
            let results =
                suites
                |> List.map buildOne
                |> List.partition (function Ok _ -> true | _ -> false)
            
            match results with
            | (successes, []) ->
                Ok (successes |> List.choose (function Ok s -> Some s | _ -> None))
            | (_successes, failures) ->
                let errors = failures |> List.choose (function Error e -> Some e | _ -> None)
                Error (String.concat "\n" errors)
        else
            // Parallel build
            let results =
                suites
                |> List.Parallel.map buildOne
                |> Array.toList
                |> List.partition (function Ok _ -> true | _ -> false)
            
            match results with
            | (successes, []) ->
                Ok (successes |> List.choose (function Ok s -> Some s | _ -> None))
            | (_successes, failures) ->
                let errors = failures |> List.choose (function Error e -> Some e | _ -> None)
                Error (String.concat "\n" errors)
    
    /// Run a test suite
    let runSuite 
        (ctx: BuildContext)
        (suite: Suites.TestSuite)
        : Result<int, string> =  // Returns: test count passed (or error)
        
        try
            let projectPath = Path.Combine(ctx.RootPath, suite.ProjectPath)
            
            if not (File.Exists projectPath) then
                Error $"Test project not found: {projectPath}"
            else
                let testResultsDir = Paths.testResultsPath ctx
                let trxFile = Path.Combine(testResultsDir, $"{suite.Name}.trx")
                
                let args = [
                    "test"
                    projectPath
                    "-c"; ctx.Config.Configuration.ToString()
                    "--no-build"
                    "--logger"; $"trx;LogFileName={trxFile}"
                    "--verbosity"; "minimal"
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
                let output = proc.StandardOutput.ReadToEnd()
                let _errors = proc.StandardError.ReadToEnd()
                proc.WaitForExit()
                
                if proc.ExitCode <> 0 then
                    Error $"Tests failed (exit {proc.ExitCode})"
                else
                    // Try to extract test count from output
                    let testMatch = 
                        System.Text.RegularExpressions.Regex.Match(output, "(\d+) passed")
                    let count = 
                        if testMatch.Success then 
                            Int32.Parse(testMatch.Groups.[1].Value)
                        else 
                            0
                    Ok count
        
        with ex ->
            Error $"Execution exception: {ex.Message}"
    
    /// Run multiple test suites
    let runAll 
        (ctx: BuildContext)
        (suites: Suites.TestSuite list)
        (parallel: int)
        : Result<(Suites.TestSuite * int) list, string> =
        
        let runOne (suite: Suites.TestSuite) : Result<(Suites.TestSuite * int), string> =
            match runSuite ctx suite with
            | Ok count -> Ok (suite, count)
            | Error e -> Error $"{suite.Name}: {e}"
        
        if parallel = 1 then
            // Sequential run
            let results =
                suites
                |> List.map runOne
                |> List.partition (function Ok _ -> true | _ -> false)
            
            match results with
            | (successes, []) ->
                Ok (successes |> List.choose (function Ok r -> Some r | _ -> None))
            | (_successes, failures) ->
                let errors = failures |> List.choose (function Error e -> Some e | _ -> None)
                Error (String.concat "\n" errors)
        else
            // Parallel run (tests can run in parallel)
            let results =
                suites
                |> List.Parallel.map runOne
                |> Array.toList
                |> List.partition (function Ok _ -> true | _ -> false)
            
            match results with
            | (successes, []) ->
                Ok (successes |> List.choose (function Ok r -> Some r | _ -> None))
            | (_successes, failures) ->
                let errors = failures |> List.choose (function Error e -> Some e | _ -> None)
                Error (String.concat "\n" errors)

module Status =
    
    /// Report test results summary
    let reportResults (results: (Suites.TestSuite * int) list) : string =
        let totalTests = results |> List.map snd |> List.sum
        sprintf "Tests Passed: %d across %d suites" totalTests results.Length
    
    /// Detailed results
    let detailResults (results: (Suites.TestSuite * int) list) : string list =
        results
        |> List.map (fun (suite, count) -> 
            sprintf "  ✓ %s: %d tests passed" suite.Name count
        )
