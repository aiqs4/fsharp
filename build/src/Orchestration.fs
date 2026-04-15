/// Orchestration targets - high-level build workflows  
/// Chains multiple phases together for convenient build scenarios

namespace FSharpBuild.Orchestration

open Fake.Core
open FSharpBuild.Types

module Workflows =
    
    /// Convenience target: full Release build + test
    /// Usage: dotnet fake build -t build:full
    let initializeFullWorkflows () =
        
        // Full: Bootstrap → Compiler → Test
        Target.create "build:full" (fun _ ->
            printfn "→ Full build workflow..."
            printfn "  1. Bootstrap"
            printfn "  2. Compiler"
            printfn "  3. Test"
        )
        
        // Quick: Just compiler (assumes bootstrap valid)
        Target.create "build:quick" (fun _ ->
            printfn "→ Quick build (compiler only)..."
        )
        
        // Release: Bootstrap (Release config) → Compiler → Test → Publish
        Target.create "build:release" (fun _ ->
            printfn "→ Release build..."
            printfn "  1. Bootstrap (Release)"
            printfn "  2. Compiler (Release)"
            printfn "  3. Test (Release)"
            printfn "  4. Publish"
        )
        
        // CI: Full Release + thorough testing
        Target.create "build:ci" (fun _ ->
            printfn "→ CI build (thorough)..."
            printfn "  1. Bootstrap (Release)"
            printfn "  2. Compiler (Release)"
            printfn "  3. All tests"
            printfn "  4. Publish"
        )
        
        // Dependencies
        "build:full" |> depends_on "test"
        "build:quick" |> depends_on "compiler"
        "build:release" |> depends_on "publish"
        "build:ci" |> depends_on "publish"

module Utilities =
    
    /// Convenience target: show build status
    let initializeUtilityTargets () =
        
        Target.create "status" (fun _ ->
            printfn "Current Build Status:"
            printfn ""
            printfn "Bootstrap:"
            Fake.Core.Shell.Exec("dotnet", "fake build -t bootstrap:status") |> ignore
        )
        
        Target.create "clean" (fun _ ->
            printfn "Cleaning artifacts..."
            "clean:all" |> Fake.Core.Target.run
        )
        
        Target.create "clean:all" (fun _ ->
            "bootstrap:clean" |> Fake.Core.Target.run
            printfn "✓ Clean complete"
        )
        
        Target.create "help" (fun _ ->
            printfn """
F# Compiler Build System (FAKE)
===============================

Core Targets:
  bootstrap         - Build Proto compiler
  compiler          - Build F# compiler (library stack)
  test              - Build & run test suites
  publish           - Create distribution artifacts

Bootstrap Targets:
  bootstrap:status  - Show bootstrap status
  bootstrap:clean   - Clean bootstrap
  bootstrap:rebuild - Force rebuild bootstrap

Compiler Targets:
  compiler:core     - Build FSharp.Core only
  compiler:service  - Build FSharp.Compiler.Service
  compiler:build    - Build FSharp.Build
  compiler:status   - Show project graph
  compiler:parallel - Parallel compiler build

Test Targets:
  test:list         - List available test suites  
  test:build        - Build all test suites
  test:run          - Run all tests
  test:quick        - Quick test (ComponentTests only)
  test:unit         - Run unit tests
  test:integration  - Run integration tests
  test:run:component   - Run ComponentTests
  test:run:service     - Run ServiceTests
  test:run:scripting   - Run ScriptingTests

Publish Targets:
  publish:nuget     - Pack NuGet libraries
  publish:exe       - Publish executables
  publish           - Full publish (nuget + exe)

Workflow Targets:
  build:full        - Bootstrap → Compiler → Test
  build:quick       - Compiler (quick rebuil)
  build:release     - Full Release build
  build:ci          - Full CI build

Utilities:
  status            - Show build status
  clean             - Clean artifacts
  clean:all         - Deep clean
  help              - This help text

Examples:
  dotnet fake build                          # Run default target (build:full)
  dotnet fake build -t compiler              # Build compiler only
  dotnet fake build -t test:quick            # Quick test
  dotnet fake build -t publish               # Publish (requires test to pass)
  dotnet fake build -t compiler:parallel     # Parallel compiler build
            """)
        )

module Initialization =
    
    let initializeAll () =
        Workflows.initializeFullWorkflows ()
        Utilities.initializeUtilityTargets ()
