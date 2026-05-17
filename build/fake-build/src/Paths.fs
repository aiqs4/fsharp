/// Path management and computation for build artifacts and sources
/// Centralizes all path logic to avoid mistakes and duplication

namespace FSharpBuild.Paths

open System.IO
open FSharpBuild.Types

module Paths =
    
    /// Get the workspace root directory
    /// Searches up from current directory until finding a directory with a .sln file
    let findRoot (startPath: string) : string option =
        let rec search (path: string) : string option =
            if path = "/" then None
            else
                let slnFiles = Directory.GetFiles(path, "*.sln")
                if Array.length slnFiles > 0 then Some path
                else search (Directory.GetParent(path).FullName)
        
        search (Path.GetFullPath startPath)
    
    /// Compute artifact paths relative to root
    let computePaths (root: string) : BuildContext -> BuildContext =
        fun ctx ->
            {
                ctx with
                    RootPath = root
                    ArtifactsPath = Path.Combine(root, "artifacts")
            }
    
    /// Get path to a project file by name
    /// Example: projectPath root "FSharp.Compiler.Service" -> "src/Compiler/Service/..."
    let projectPath (root: string) (projectName: string) : string option =
        // Search for .fsproj files matching the name
        let searchPattern = $"{projectName}.fsproj"
        let results = Directory.GetFiles(root, searchPattern, SearchOption.AllDirectories)
        
        if Array.length results > 0 then
            Some results[0]
        else
            None
    
    /// Get output directory for a configuration
    let outputDir (ctx: BuildContext) (component: Component) : string =
        let binDir = ctx.BinPath
        let config = ctx.Config.Configuration.ToString()
        let platform = ctx.Config.Platform.ToString()
        
        Path.Combine(binDir, config, platform, component.ToString())
    
    /// Get bootstrap directory
    let bootstrapDir (ctx: BuildContext) : string =
        ctx.BootstrapPath
    
    /// Get bootstrap compiler path
    let bootstrapFsc (ctx: BuildContext) : string =
        let fscName = if System.Environment.OSVersion.Platform = System.PlatformID.Win32NT then "fsc.exe" else "fsc"
        Path.Combine(bootstrapDir ctx, fscName)
    
    /// Get SDK tools path (for Proto fallback)
    let dotnetPath () : string =
        "dotnet"  // Assumes "dotnet" is in PATH
    
    /// Validate that required paths exist
    let validatePaths (ctx: BuildContext) : Result<unit, string> =
        let requireDir path name =
            if Directory.Exists path then Ok ()
            else Error $"Required directory not found: {name} at {path}"
        
        let requireFile path name =
            if File.Exists path then Ok ()
            else Error $"Required file not found: {name} at {path}"
        
        [ 
            requireDir ctx.RootPath "root"
            requireDir (Path.Combine(ctx.RootPath, "src")) "src"
            requireDir (Path.Combine(ctx.RootPath, "tests")) "tests"
        ]
        |> List.collect (function Ok () -> [] | Error e -> [e])
        |> function
            | [] -> Ok ()
            | errors -> Error (String.concat "\n" errors)
    
    /// Get solution file path
    let solutionPath (root: string) (name: string) : string option =
        let slnFile = Path.Combine(root, $"{name}.sln")
        if File.Exists slnFile then Some slnFile else None
    
    /// Get all solution files
    let allSolutions (root: string) : (string * string) list =
        // (solution name, full path)
        [
            ("FSharp", Path.Combine(root, "FSharp.sln"))
            ("FSharp.Compiler.Service", Path.Combine(root, "FSharp.Compiler.Service.sln"))
            ("FSharp.Benchmarks", Path.Combine(root, "FSharp.Benchmarks.sln"))
            ("LSPSolutionSlim", Path.Combine(root, "LSPSolutionSlim.sln"))
        ]
        |> List.filter (fun (_, path) -> File.Exists path)
    
    /// Get test results path
    let testResultsPath (ctx: BuildContext) : string =
        Path.Combine(ctx.ArtifactsPath, "test-results")
    
    /// Create all necessary directories for a build
    let ensureDirectories (ctx: BuildContext) : unit =
        [
            ctx.BinPath
            ctx.ObjPath
            ctx.BootstrapPath
            testResultsPath ctx
        ]
        |> List.iter (fun dir -> 
            if not (Directory.Exists dir) then
                Directory.CreateDirectory dir |> ignore
        )
    
    /// Clean build artifacts (with options)
    type CleanMode =
        | Partial      // Only remove bin/obj for current config
        | Full         // Remove all artifacts
        | Bootstrap    // Only remove bootstrap artifacts
    
    let clean (ctx: BuildContext) (mode: CleanMode) : unit =
        let paths = 
            match mode with
            | Partial -> [outputDir ctx Component.Service; ctx.ObjPath]
            | Full -> [ctx.BinPath; ctx.ObjPath]
            | Bootstrap -> [bootstrapDir ctx]
        
        paths
        |> List.iter (fun path ->
            if Directory.Exists path then
                Directory.Delete(path, true)
        )

module Projects =
    
    /// Map component to its directory relative to src/
    let componentSourceDir (component: Component) : string =
        match component with
        | Component.Service -> "src/Compiler/Service"
        | Component.Build -> "src/Compiler/Build"
        | Component.Core -> "src/FSharp.Core"
        | Component.Fsc -> "src/fsc/fscProject"
        | Component.Fsi -> "src/fsi"
        | Component.ComponentTests -> "tests/FSharp.Compiler.ComponentTests"
        | Component.ServiceTests -> "tests/FSharp.Compiler.Service.Tests"
        | Component.ScriptingTests -> "tests/FSharp.Compiler.Private.Scripting.UnitTests"
        | Component.BuildTests -> "tests/FSharp.Build.UnitTests"
        | Component.CoreTests -> "tests/FSharp.Core.UnitTests"
        | Component.SuiteTests -> "tests/FSharpSuite.Tests"
    
    /// Get project file for a component
    let projectFile (root: string) (component: Component) : string =
        let dir = Path.Combine(root, componentSourceDir component)
        let projectName =
            match component with
            | Component.Service -> "FSharp.Compiler.Service"
            | Component.Build -> "FSharp.Build"
            | Component.Core -> "FSharp.Core"
            | Component.Fsc -> "fsc"
            | Component.Fsi -> "fsi"
            | Component.ComponentTests -> "FSharp.Compiler.ComponentTests"
            | Component.ServiceTests -> "FSharp.Compiler.Service.Tests"
            | Component.ScriptingTests -> "FSharp.Compiler.Private.Scripting.UnitTests"
            | Component.BuildTests -> "FSharp.Build.UnitTests"
            | Component.CoreTests -> "FSharp.Core.UnitTests"
            | Component.SuiteTests -> "FSharpSuite.Tests"
        
        Path.Combine(dir, $"{projectName}.fsproj")
    
    /// Get all component projects
    let allComponents : Component list =
        [
            // Compiler stack (phase order matters)
            Component.Core
            Component.Service
            Component.Build
            Component.Fsc
            Component.Fsi
            // Tests
            Component.ComponentTests
            Component.ServiceTests
            Component.ScriptingTests
            Component.BuildTests
            Component.CoreTests
            Component.SuiteTests
        ]
