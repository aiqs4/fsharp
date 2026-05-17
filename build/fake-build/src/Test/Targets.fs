/// FAKE build targets for test phase
/// Defines targets for building and running test suites

namespace FSharpBuild.Test

open System
open Fake.Core
open FSharpBuild.Types

module Targets =
    
    let private createTestTargets () =
        let ctx = {
            Config = { Configuration = Debug; Platform = AnyCPU; TargetFramework = None }
            RootPath = System.Environment.CurrentDirectory
            ArtifactsPath = System.IO.Path.Combine(System.Environment.CurrentDirectory, "artifacts")
            ProjectsCache = None
            Results = []
        }
        
        // List available test suites
        Target.create "test:list" (fun _ ->
            Status.reportAll ()
        )
        
        // Build all tests
        Target.create "test:build" (fun _ ->
            printfn "→ Building test suites..."
            let parallel = Environment.ProcessorCount
            match Logic.buildAll ctx Suites.buildOrder parallel with
            | Ok suites ->
                printfn "✓ Built %d test suites" suites.Length
            | Error e ->
                printfn "✗ Build failed: %s" e
                failwith "Test build failed"
        )
        
        // Build specific test suites
        for suite in Suites.all do
            Target.create $"test:build:{suite.Name.ToLower()}" (fun _ ->
                printfn $"→ Building {suite.Name}..."
                match Logic.buildSuite ctx suite with
                | Ok duration ->
                    printfn $"✓ Built in %.1fs" duration.TotalSeconds
                | Error e ->
                    printfn $"✗ Build failed: {e}"
                    failwith $"{suite.Name} build failed"
            )
        
        // Run all tests
        Target.create "test:run" (fun _ ->
            printfn "→ Running all tests (parallel)..."
            let parallel = Environment.ProcessorCount
            
            match Logic.runAll ctx Suites.executionOrder parallel with
            | Ok results ->
                let summary = Status.reportResults results
                printfn "✓ %s" summary
                Status.detailResults results |> List.iter printfn
            | Error e ->
                printfn "✗ Tests failed: %s" e
                failwith "Test execution failed"
        )
        
        // Run specific test suite
        for suite in Suites.all do
            Target.create $"test:run:{suite.Name.ToLower()}" (fun _ ->
                printfn $"→ Running {suite.Name}..."
                match Logic.runSuite ctx suite with
                | Ok count ->
                    printfn $"✓ {count} tests passed"
                | Error e ->
                    printfn $"✗ Tests failed: {e}"
                    failwith $"{suite.Name} tests failed"
            )
        
        // Quick test - ComponentTests only
        Target.create "test:quick" (fun _ ->
            printfn "→ Running ComponentTests (quick)..."
            let suite = Suites.getSuite "ComponentTests"
            match suite with
            | Some s ->
                match Logic.runSuite ctx s with
                | Ok count ->
                    printfn $"✓ {count} tests passed"
                | Error e ->
                    failwith $"ComponentTests failed: {e}"
            | None ->
                failwith "ComponentTests suite not found"
        )
        
        // Unit tests only
        Target.create "test:unit" (fun _ ->
            printfn "→ Running unit tests..."
            let unitSuites = Suites.inCategory "UnitTests"
            let parallel = Environment.ProcessorCount
            
            match Logic.runAll ctx unitSuites parallel with
            | Ok results ->
                let summary = Status.reportResults results
                printfn "✓ %s" summary
            | Error e ->
                failwith $"Unit tests failed: {e}"
        )
        
        // Integration tests
        Target.create "test:integration" (fun _ ->
            printfn "→ Running integration tests..."
            let intTests = Suites.inCategory "IntegrationTests"
            
            match Logic.runAll ctx intTests 1 with
            | Ok results ->
                let summary = Status.reportResults results
                printfn "✓ %s" summary
            | Error e ->
                failwith $"Integration tests failed: {e}"
        )
        
        // Full pipeline: build + run all tests
        Target.create "test" (fun _ ->
            printfn "→ Test phase: build & run..."
            
            // Build
            match Logic.buildAll ctx Suites.buildOrder 1 with
            | Ok _builtSuites ->
                // Run
                let parallel = Environment.ProcessorCount
                match Logic.runAll ctx Suites.executionOrder parallel with
                | Ok results ->
                    let summary = Status.reportResults results
                    printfn "✓ %s" summary
                    Status.detailResults results |> List.iter printfn
                | Error e ->
                    failwith $"Test execution failed: {e}"
            | Error e ->
                failwith $"Test build failed: {e}"
        )
    
    let private registerDependencies () =
        // All test:run targets depend on test:build
        for suite in Suites.all do
            $"test:run:{suite.Name.ToLower()}" |> depends_on $"test:build:{suite.Name.ToLower()}"
        
        // test:run depends on test:build
        "test:run" |> depends_on "test:build"
        
        // test target runs everything
        "test" |> depends_on "test:build"
        "test" |> depends_on "test:run"
        
        // test:build depends on compiler being done
        "test:build" |> depends_on "compiler"
    
    let initializeTargets () =
        createTestTargets ()
        registerDependencies ()
