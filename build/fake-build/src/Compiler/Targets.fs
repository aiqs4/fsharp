/// FAKE build targets for compiler phase
/// Defines fine-grained targets for building different compiler components

namespace FSharpBuild.Compiler

open System
open Fake.Core
open FSharpBuild.Types
open FSharpBuild.Compiler.ProjectDAG
open FSharpBuild.Compiler.Logic
open FSharpBuild.Paths

module Targets =
    
    let private dag = ref None
    
    /// Initialize DAG (lazy load)
    let private getDag () : ProjectDAG.DepGraph =
        match !dag with
        | Some d -> d
        | None ->
            let root = System.Environment.CurrentDirectory
            let projects = ProjectDAG.discoverProjects root
            let d = ProjectDAG.buildGraph projects
            dag := Some d
            d
    
    let private createCompilerTargets () =
        let ctx = {
            Config = { Configuration = Debug; Platform = AnyCPU; TargetFramework = None }
            RootPath = System.Environment.CurrentDirectory
            ArtifactsPath = Path.Combine(System.Environment.CurrentDirectory, "artifacts")
            ProjectsCache = None
            Results = []
        }
        
        // Status target - show DAG
        Target.create "compiler:status" (fun _ ->
            let d = getDag ()
            printfn "Compiler Build Status:"
            printfn "  Total projects: %d" d.AllProjects.Length
            printfn "  Layers:"
            for KeyValue(layer, projs) in d.ProjectsByLayer do
                printfn "    • %A: %d projects" layer projs.Length
            ProjectDAG.printGraph d
        )
        
        // Core library target
        Target.create "compiler:core" (fun _ ->
            printfn "→ Building FSharp.Core..."
            let d = getDag ()
            let coreProj = 
                d.AllProjects 
                |> List.find (fun p -> p.Name = "FSharp.Core")
            let results = Logic.buildProjects ctx [coreProj] 1
            match results with
            | [r] when r.Success ->
                printfn "✓ FSharp.Core built"
            | _ ->
                printfn "✗ FSharp.Core build failed"
                failwith "Core build failed"
        )
        
        // Service library target
        Target.create "compiler:service" (fun _ ->
            printfn "→ Building FSharp.Compiler.Service..."
            let d = getDag ()
            let deps = ProjectDAG.getDependencies d Component.Service
            let service = 
                d.AllProjects 
                |> List.find (fun p -> p.Name = "FSharp.Compiler.Service")
            
            // Build dependencies first
            if deps.Length > 0 then
                let depResults = Logic.buildProjects ctx deps 1
                let allSuccess = depResults |> List.forall (fun r -> r.Success)
                if not allSuccess then failwith "Dependency build failed"
            
            // Build service
            let results = Logic.buildProjects ctx [service] 1
            match results with
            | [r] when r.Success ->
                printfn "✓ FSharp.Compiler.Service built"
            | _ ->
                printfn "✗ FSharp.Compiler.Service build failed"
                failwith "Service build failed"
        )
        
        // Build library target
        Target.create "compiler:build" (fun _ ->
            printfn "→ Building FSharp.Build..."
            let d = getDag ()
            let build = 
                d.AllProjects 
                |> List.tryFind (fun p -> p.Name = "FSharp.Build")
            
            match build with
            | Some proj ->
                let results = Logic.buildProjects ctx [proj] 1
                match results with
                | [r] when r.Success ->
                    printfn "✓ FSharp.Build built"
                | _ ->
                    printfn "✗ FSharp.Build build failed"
                    failwith "Build library failed"
            | None ->
                printfn "⚠ FSharp.Build not found"
        )
        
        // Main compiler target - build in dependency order
        Target.create "compiler" (fun _ ->
            printfn "→ Building F# Compiler..."
            let d = getDag ()
            
            match Logic.buildAll ctx d 1 with
            | Ok results ->
                let summary = Status.reportResults results
                printfn "✓ %s" summary
                
                let failed = results |> List.filter (fun r -> not r.Success)
                if failed.Length > 0 then
                    printfn "\n✗ %d build(s) failed:" failed.Length
                    Status.detailReport failed 
                    |> List.iter printfn
                    failwith "Compilation failed"
            
            | Error e ->
                printfn "✗ Build error: %s" e
                failwith e
        )
        
        // Parallel build target
        Target.create "compiler:parallel" (fun _ ->
            printfn "→ Building F# Compiler (parallel)..."
            let d = getDag ()
            let parallel = Environment.ProcessorCount
            
            match Logic.buildAll ctx d parallel with
            | Ok results ->
                let summary = Status.reportResults results
                printfn "✓ %s" summary
                
                let failed = results |> List.filter (fun r -> not r.Success)
                if failed.Length > 0 then
                    failwith "Parallel compilation failed"
            
            | Error e ->
                failwith $"Build error: {e}"
        )
    
    let private registerDependencies () =
        // Service depends on Core
        "compiler:service" |> depends_on "compiler:core"
        
        // Build depends on Service
        "compiler:build" |> depends_on "compiler:service"
        
        // Main compiler target (sequential)
        // compiler depends on bootstrap first
        "compiler" |> depends_on "bootstrap"
    
    let initializeTargets () =
        createCompilerTargets ()
        registerDependencies ()
