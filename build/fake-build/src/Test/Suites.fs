/// Test suite definitions for F# Compiler
/// Defines all 6 test suites with their metadata and build paths

namespace FSharpBuild.Test

open System.IO
open FSharpBuild.Types

module Suites =
    
    /// Test suite metadata
    type TestSuite =
        {
            Name: string
            Component: Component
            ProjectPath: string  // RelativePath from root
            Description: string
            Category: string     // UnitTests | ComponentTests | IntegrationTests
        }
    
    /// All 6 test suites in the F# Compiler project
    let all : TestSuite list = [
        {
            Name = "ComponentTests"
            Component = Component.ComponentTests
            ProjectPath = "tests/FSharp.Compiler.ComponentTests/FSharp.Compiler.ComponentTests.fsproj"
            Description = "Main compiler component tests with IL baselines"
            Category = "ComponentTests"
        }
        {
            Name = "ServiceTests"
            Component = Component.ServiceTests
            ProjectPath = "tests/FSharp.Compiler.Service.Tests/FSharp.Compiler.Service.Tests.fsproj"
            Description = "Language service and IDE feature tests"
            Category = "UnitTests"
        }
        {
            Name = "ScriptingTests"
            Component = Component.ScriptingTests
            ProjectPath = "tests/FSharp.Compiler.Private.Scripting.UnitTests/FSharp.Compiler.Private.Scripting.UnitTests.fsproj"
            Description = "FSI interactive scripting tests"
            Category = "UnitTests"
        }
        {
            Name = "BuildTests"
            Component = Component.BuildTests
            ProjectPath = "tests/FSharp.Build.UnitTests/FSharp.Build.UnitTests.fsproj"
            Description = "MSBuild integration tests"
            Category = "UnitTests"
        }
        {
            Name = "CoreTests"
            Component = Component.CoreTests
            ProjectPath = "tests/FSharp.Core.UnitTests/FSharp.Core.UnitTests.fsproj"
            Description = "FSharp.Core library tests"
            Category = "UnitTests"
        }
        {
            Name = "SuiteTests"
            Component = Component.SuiteTests
            ProjectPath = "tests/FSharpSuite.Tests/FSharpSuite.Tests.fsproj"
            Description = "Full test suite runner"
            Category = "IntegrationTests"
        }
    ]
    
    /// Get suite by name
    let getSuite (name: string) : TestSuite option =
        all |> List.tryFind (fun s -> s.Name.ToLower() = name.ToLower())
    
    /// Get suite by component
    let getByComponent (component: Component) : TestSuite option =
        all |> List.tryFind (fun s -> s.Component = component)
    
    /// Get all suites in category
    let inCategory (category: string) : TestSuite list =
        all |> List.filter (fun s -> s.Category = category)
    
    /// Build order for test suites (dependencies first)
    let buildOrder : TestSuite list =
        // ComponentTests first (most important), then others in parallel-safe order
        [
            getSuite "ComponentTests"
            getSuite "ServiceTests"
            getSuite "ScriptingTests"
            getSuite "BuildTests"
            getSuite "CoreTests"
            getSuite "SuiteTests"
        ]
        |> List.choose id
    
    /// Test execution order (can run many in parallel)
    let executionOrder : TestSuite list =
        buildOrder

module Filtering =
    
    /// Parse test filter expression
    /// Examples: "component", "service", "all", "unit", "integration"
    let parseFilter (expr: string) : TestSuite list =
        let lower = expr.ToLower()
        
        match lower with
        | "component" | "componenttests" -> 
            [Suites.getSuite "ComponentTests"] |> List.choose id
        | "service" | "servicetests" -> 
            [Suites.getSuite "ServiceTests"] |> List.choose id
        | "scripting" -> 
            [Suites.getSuite "ScriptingTests"] |> List.choose id
        | "build" | "buildtests" -> 
            [Suites.getSuite "BuildTests"] |> List.choose id
        | "core" | "coretests" -> 
            [Suites.getSuite "CoreTests"] |> List.choose id
        | "suite" | "suitetests" -> 
            [Suites.getSuite "SuiteTests"] |> List.choose id
        | "unit" -> Suites.inCategory "UnitTests"
        | "integration" -> Suites.inCategory "IntegrationTests"
        | "all" -> Suites.all
        | _ -> 
            // Try exact match
            match Suites.getSuite expr with
            | Some s -> [s]
            | None -> []
    
    /// Parse comma-separated filters
    let parseMultiple (expr: string) : TestSuite list =
        expr.Split(',')
        |> Array.map (fun s -> s.Trim())
        |> Array.collect (fun s -> parseFilter s |> List.toArray)
        |> Seq.distinct
        |> Seq.toList

module Status =
    
    /// Report test suite status
    let reportAll () : unit =
        printfn "F# Compiler Test Suites:"
        Suites.all
        |> List.iter (fun suite ->
            printfn "  • %-20s %s" suite.Name suite.Description
            printfn "    Path: %s" suite.ProjectPath
            printfn "    Category: %s" suite.Category
        )
