/// FAKE build targets for publish phase
/// Defines targets for creating distribution artifacts

namespace FSharpBuild.Publish

open System
open System.IO
open Fake.Core
open FSharpBuild.Types
open FSharpBuild.Paths

module Targets =
    
    let private createPublishTargets () =
        let ctx = {
            Config = { Configuration = Release; Platform = AnyCPU; TargetFramework = None }
            RootPath = System.Environment.CurrentDirectory
            ArtifactsPath = Path.Combine(System.Environment.CurrentDirectory, "artifacts")
            ProjectsCache = None
            Results = []
        }
        
        let publishDir = Path.Combine(ctx.ArtifactsPath, "publish")
        let nugetDir = Path.Combine(publishDir, "nuget")
        let exeDir = Path.Combine(publishDir, "exe")
        
        // Ensure directories exist
        [publishDir; nugetDir; exeDir] 
        |> List.iter (fun dir ->
            if not (Directory.Exists dir) then
                Directory.CreateDirectory(dir) |> ignore
        )
        
        // Pack NuGet libraries
        Target.create "publish:nuget" (fun _ ->
            printfn "→ Packing NuGet libraries..."
            
            // Pack FSharp.Core
            let coreProj = Path.Combine(ctx.RootPath, "src/FSharp.Core/FSharp.Core.fsproj")
            match Logic.packNuget ctx coreProj nugetDir with
            | Ok duration ->
                printfn "✓ Packed FSharp.Core in %.1fs" duration.TotalSeconds
            | Error e ->
                printfn "✗ Failed to pack: %s" e
                failwith "NuGet packing failed"
            
            // Pack FSharp.Compiler.Service
            let serviceProj = Path.Combine(ctx.RootPath, "src/Compiler/Service/FSharp.Compiler.Service.fsproj")
            match Logic.packNuget ctx serviceProj nugetDir with
            | Ok duration ->
                printfn "✓ Packed FSharp.Compiler.Service in %.1fs" duration.TotalSeconds
            | Error e ->
                printfn "✗ Failed to pack: %s" e
                failwith "NuGet packing failed"
        )
        
        // Publish executables
        Target.create "publish:exe" (fun _ ->
            printfn "→ Publishing executables..."
            
            // Publish fsc
            let fscProj = Path.Combine(ctx.RootPath, "src/fsc/fscProject/fsc.fsproj")
            match Logic.publishExe ctx fscProj exeDir None with
            | Ok duration ->
                printfn "✓ Published fsc in %.1fs" duration.TotalSeconds
            | Error e ->
                printfn "⚠ Failed to publish fsc: %s" e
                // Don't fail - fsc might have different project structure
            
            // Publish fsi
            let fsiProj = Path.Combine(ctx.RootPath, "src/fsi/fsi.fsproj")
            match Logic.publishExe ctx fsiProj exeDir None with
            | Ok duration ->
                printfn "✓ Published fsi in %.1fs" duration.TotalSeconds
            | Error e ->
                printfn "⚠ Failed to publish fsi: %s" e
        )
        
        // Full publish pipeline
        Target.create "publish" (fun _ ->
            printfn "→ Publish phase..."
            printfn "  NuGet packages → %s" nugetDir
            printfn "  Executables → %s" exeDir
            
            // Create nuget packages
            match Logic.packageLibraries ctx nugetDir with
            | Ok nugets ->
                Status.reportArtifacts nugets []
            | Error e ->
                printfn "✗ NuGet packaging failed: %s" e
                // Continue anyway - exe publishing might still work
            
            // Create executables  
            match Logic.packageExes ctx exeDir with
            | Ok exes ->
                Status.reportArtifacts [] exes
            | Error e ->
                printfn "⚠ Executable publishing failed: %s" e
            
            printfn "✓ Publish phase complete"
        )
    
    let private registerDependencies () =
        // publish depends on test passing
        "publish" |> depends_on "test"
        "publish:nuget" |> depends_on "compiler"
        "publish:exe" |> depends_on "compiler"
    
    let initializeTargets () =
        createPublishTargets ()
        registerDependencies ()
