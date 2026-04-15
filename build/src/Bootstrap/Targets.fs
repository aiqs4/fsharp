/// FAKE build targets for bootstrap phase
/// Defines the Fake.DotNet targets and task wiring for bootstrap

namespace FSharpBuild.Bootstrap

open Fake.Core
open Fake.DotNet
open FSharpBuild.Types
open FSharpBuild.Bootstrap.Logic
open FSharpBuild.Paths

module Targets =
    
    let private createBootstrapTargets () =
        // Report bootstrap status
        Target.create "bootstrap:status" (fun _ ->
            let ctx = {
                Config = { Configuration = Debug; Platform = AnyCPU; TargetFramework = None }
                RootPath = System.Environment.CurrentDirectory
                ArtifactsPath = Path.Combine(System.Environment.CurrentDirectory, "artifacts")
                ProjectsCache = None
                Results = []
            }
            printfn "%s" (Status.report ctx)
        )
        
        // Clean bootstrap artifacts
        Target.create "bootstrap:clean" (fun _ ->
            let ctx = {
                Config = { Configuration = Debug; Platform = AnyCPU; TargetFramework = None }
                RootPath = System.Environment.CurrentDirectory
                ArtifactsPath = Path.Combine(System.Environment.CurrentDirectory, "artifacts")
                ProjectsCache = None
                Results = []
            }
            printfn "Cleaning bootstrap artifacts..."
            Logic.cleanBootstrap ctx
            printfn "✓ Bootstrap cleaned"
        )
        
        // Force rebuild bootstrap
        Target.create "bootstrap:rebuild" (fun _ ->
            let ctx = {
                Config = { Configuration = Debug; Platform = AnyCPU; TargetFramework = None }
                RootPath = System.Environment.CurrentDirectory
                ArtifactsPath = Path.Combine(System.Environment.CurrentDirectory, "artifacts")
                ProjectsCache = None
                Results = []
            }
            Logic.cleanBootstrap ctx
            match Logic.buildBootstrap ctx with
            | Ok duration ->
                printfn "✓ Bootstrap built successfully in %.1fs" duration.TotalSeconds
                match Logic.validateBootstrap ctx with
                | Ok () -> printfn "✓ Bootstrap validation passed"
                | Error e -> failwith $"Bootstrap validation failed: {e}"
            | Error e -> failwith $"Bootstrap build failed: {e}"
        )
        
        // Main bootstrap target (check then build if needed)
        Target.create "bootstrap" (fun _ ->
            let ctx = {
                Config = { Configuration = Debug; Platform = AnyCPU; TargetFramework = None }
                RootPath = System.Environment.CurrentDirectory
                ArtifactsPath = Path.Combine(System.Environment.CurrentDirectory, "artifacts")
                ProjectsCache = None
                Results = []
            }
            
            let status = Status.getStatus ctx
            
            match status with
            | Status.Valid ->
                printfn "✓ Using cached bootstrap"
            | Status.Stale ->
                printfn "⟳ Bootstrap outdated, rebuilding..."
                match Logic.buildBootstrap ctx with
                | Ok duration ->
                    printfn "✓ Bootstrap rebuilt in %.1fs" duration.TotalSeconds
                    match Logic.validateBootstrap ctx with
                    | Ok () -> ()
                    | Error e -> failwith $"Bootstrap validation failed: {e}"
                | Error e -> failwith $"Bootstrap build failed: {e}"
            | Status.New ->
                printfn "→ Building bootstrap compiler..."
                match Logic.buildBootstrap ctx with
                | Ok duration ->
                    printfn "✓ Bootstrap built in %.1fs" duration.TotalSeconds
                    match Logic.validateBootstrap ctx with
                    | Ok () -> ()
                    | Error e -> failwith $"Bootstrap validation failed: {e}"
                | Error e -> failwith $"Bootstrap build failed: {e}"
            | Status.Invalid ->
                failwith "Bootstrap build previously failed - run 'dotnet fake build -t bootstrap:rebuild' to retry"
        )
    
    let private registerDependencies () =
        // bootstrap:rebuild depends on bootstrap:clean
        "bootstrap:rebuild" |> depends_on "bootstrap:clean"
        
        // bootstrap:status can run independently
        // bootstrap can run independently
    
    let initializeTargets () =
        createBootstrapTargets ()
        registerDependencies ()
