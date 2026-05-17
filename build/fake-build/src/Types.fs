/// Core type definitions for the F# Compiler build system
/// All modules depend on these types for configuration, projects, and build state

namespace FSharpBuild.Types

open System
open System.IO

/// Build configuration: Debug, Release, or Proto (bootstrap)
type Configuration =
    | Debug
    | Release
    | Proto  // Bootstrap compiler - uses SDK to build itself
    
    override this.ToString() =
        match this with
        | Debug -> "Debug"
        | Release -> "Release"
        | Proto -> "Proto"

/// Platform architecture
type Platform =
    | AnyCPU
    | X86
    
    override this.ToString() =
        match this with
        | AnyCPU -> "Any CPU"
        | X86 -> "x86"

/// Target framework for compilation
type TargetFramework =
    | Net472           // .NET Framework 4.7.2
    | NetStandard20    // netstandard2.0
    | NetStandard21    // netstandard2.1
    | Net10            // net10.0 (new)
    
    override this.ToString() =
        match this with
        | Net472 -> "net472"
        | NetStandard20 -> "netstandard2.0"
        | NetStandard21 -> "netstandard2.1"
        | Net10 -> "net10.0"

/// Complete build configuration
[<CustomEquality; CustomComparison>]
type BuildConfig =
    {
        Configuration: Configuration
        Platform: Platform
        TargetFramework: TargetFramework option  // None = all appropriate for project
    }
    
    member this.Identifier =
        let tfStr = match this.TargetFramework with None -> "" | Some tf -> $"/{tf}"
        $"{this.Configuration}|{this.Platform}{tfStr}"
    
    override this.ToString() = this.Identifier
    override this.Equals(obj) = 
        match obj with
        | :? BuildConfig as c -> 
            this.Configuration = c.Configuration && 
            this.Platform = c.Platform && 
            this.TargetFramework = c.TargetFramework
        | _ -> false
    override this.GetHashCode() =
        hash (this.Configuration, this.Platform, this.TargetFramework)
    
    interface IComparable with
        member this.CompareTo(obj) =
            match obj with
            | :? BuildConfig as c ->
                let cfgCmp = compare (this.Configuration.ToString()) (c.Configuration.ToString())
                if cfgCmp <> 0 then cfgCmp else
                let platCmp = compare (this.Platform.ToString()) (c.Platform.ToString())
                if platCmp <> 0 then platCmp else
                compare (this.TargetFramework) (c.TargetFramework)
            | _ -> failwith "Cannot compare with non-BuildConfig"

/// Project dependency layer in the build DAG
type BuildLayer =
    | BootstrapLayer        // Proto compiler (tier 0)
    | CoreLayer             // FSharp.Core runtime
    | CompilerLayer         // Service, Build, Interactive, etc.
    | ToolLayer             // fsc, fsi executables
    | TestLayer             // Test suites
    | BenchmarkLayer        // Performance tests

/// Project metadata for dependency tracking
type ProjectInfo =
    {
        Name: string                    // e.g., "FSharp.Compiler.Service"
        RelativePath: string            // e.g., "src/Compiler/Service/FSharp.Compiler.Service.fsproj"
        FullPath: string               // Computed: root + RelativePath
        Layer: BuildLayer
        TargetFrameworks: TargetFramework list
        Dependencies: string list      // Project names this depends on
        IsExecutable: bool
        IsTestProject: bool
        OutputExtension: string        // .dll, .exe
    }
    
    member this.IsLibrary = not this.IsExecutable && not this.IsTestProject

/// Build phase in the compilation pipeline
type BuildPhase =
    | Bootstrap             // Build Proto compiler with SDK (30-60s)
    | Compiler              // Self-compile with bootstrap (2-3 min)
    | TestBuild             // Build 6 test projects (1-2 min)
    | TestRun               // Execute all test suites (2-5 min)
    | Publish               // Create exe/nupkg distributions (1-2 min)
    
    override this.ToString() =
        match this with
        | Bootstrap -> "bootstrap"
        | Compiler -> "compiler"
        | TestBuild -> "test-build"
        | TestRun -> "test-run"
        | Publish -> "publish"

/// Sub-component target for fine-grained control
type Component =
    | Service               // FSharp.Compiler.Service
    | Build                 // FSharp.Build
    | Core                  // FSharp.Core
    | Fsc                   // fsc executable
    | Fsi                   // fsi executable
    | ComponentTests        // FSharp.Compiler.ComponentTests
    | ServiceTests          // FSharp.Compiler.Service.Tests
    | ScriptingTests        // FSharp.Compiler.Private.Scripting.UnitTests
    | BuildTests            // FSharp.Build.UnitTests
    | CoreTests             // FSharp.Core.UnitTests
    | SuiteTests            // FSharpSuite.Tests
    
    override this.ToString() =
        match this with
        | Service -> "service"
        | Build -> "build"
        | Core -> "core"
        | Fsc -> "fsc"
        | Fsi -> "fsi"
        | ComponentTests -> "component"
        | ServiceTests -> "service"
        | ScriptingTests -> "scripting"
        | BuildTests -> "build"
        | CoreTests -> "core"
        | SuiteTests -> "suite"

/// Target specification for FAKE build targets
type BuildTarget =
    {
        Phase: BuildPhase
        Component: Component option     // None = all in phase
        Parallel: int option            // Override degree of parallelism
    }
    
    /// Human-readable target identifier: phase:component or phase
    member this.Identifier =
        match this.Component with
        | None -> this.Phase.ToString()
        | Some c -> $"{this.Phase}:{c}"
    
    override this.ToString() = this.Identifier

/// Result of a build operation
type BuildResult =
    {
        Target: BuildTarget
        Success: bool
        Duration: TimeSpan
        OutputLines: string list
        ErrorCount: int
        WarningCount: int
    }
    
    member this.Summary =
        let status = if this.Success then "✓ PASS" else "✗ FAIL"
        $"{status} {this.Target.Identifier} ({this.Duration.TotalSeconds:F1}s) - Errors: {this.ErrorCount}, Warnings: {this.WarningCount}"

/// Execution context with configuration and paths
type BuildContext =
    {
        Config: BuildConfig
        RootPath: string                // Workspace root
        ArtifactsPath: string           // artifacts/
        ProjectsCache: ProjectInfo list option  // Lazy-loaded project metadata
        Results: BuildResult list       // Completed builds (for reporting)
    }
    
    /// Computed paths for artifacts
    member this.BinPath = Path.Combine(this.ArtifactsPath, "bin")
    member this.ObjPath = Path.Combine(this.ArtifactsPath, "obj")
    member this.BootstrapPath = Path.Combine(this.ArtifactsPath, "Bootstrap")
    
    /// Get or compute project info (lazy cache)
    member this.Projects = 
        match this.ProjectsCache with
        | Some projects -> projects
        | None -> []  // Will be populated by discovery phase
