/// Project dependency DAG (Directed Acyclic Graph) analysis
/// Discovers projects, builds the dependency graph, and computes build order

namespace FSharpBuild.Compiler

open System
open System.IO
open System.Xml.Linq
open FSharpBuild.Types

module ProjectDAG =
    
    /// Parse project file to extract dependencies and metadata
    let private parseProjectFile (path: string) : ProjectInfo option =
        try
            let xdoc = XDocument.Load(path)
            let root = xdoc.Root
            
            // Extract TargetFrameworks
            let tfStr = 
                root.Descendants(XName.Get("TargetFrameworks"))
                |> Seq.tryHead
                |> Option.map (fun x -> x.Value)
            
            let frameworks = 
                match tfStr with
                | Some str ->
                    str.Split(';')
                    |> Array.map (fun tf -> 
                        match tf.Trim().ToLower() with
                        | "net472" -> Some TargetFramework.Net472
                        | "netstandard2.0" -> Some TargetFramework.NetStandard20
                        | "netstandard2.1" -> Some TargetFramework.NetStandard21
                        | "net10.0" | "net10" -> Some TargetFramework.Net10
                        | _ -> None
                    )
                    |> Array.choose id
                    |> Array.toList
                | None -> [TargetFramework.Net472]
            
            // Extract OutputType
            let outputType = 
                root.Descendants(XName.Get("OutputType"))
                |> Seq.tryHead
                |> Option.map (fun x -> x.Value.ToLower())
            
            let isExe = match outputType with Some "exe" -> true | _ -> false
            let isLib = match outputType with Some "library" -> true | _ -> false
            
            // Extract project name from filename
            let projectName = Path.GetFileNameWithoutExtension(path)
            let isTest = projectName.Contains("Test") || projectName.Contains("test")
            
            // Extract project references
            let refs = 
                root.Descendants(XName.Get("ProjectReference"))
                |> Seq.map (fun ref ->
                    let include' = ref.Attribute(XName.Get("Include")).Value
                    Path.GetFileNameWithoutExtension(include')
                )
                |> Seq.toList
            
            // Determine layer
            let layer =
                if projectName = "FSharp.Core" then CoreLayer
                elif projectName.Contains("Test") then TestLayer
                elif projectName = "FSharp.Benchmarks" then BenchmarkLayer
                elif isExe || projectName = "fsc" || projectName = "fsi" then ToolLayer
                elif projectName.Contains("Service") || projectName.Contains("Compiler") then CompilerLayer
                else ToolLayer
            
            Some {
                Name = projectName
                RelativePath = path
                FullPath = Path.GetFullPath(path)
                Layer = layer
                TargetFrameworks = frameworks
                Dependencies = refs
                IsExecutable = isExe
                IsTestProject = isTest
                OutputExtension = if isExe then ".exe" else ".dll"
            }
        
        with _ ->
            None
    
    /// Discover all project files in the workspace
    let discoverProjects (root: string) : ProjectInfo list =
        try
            Directory.GetFiles(root, "*.fsproj", SearchOption.AllDirectories)
            |> Array.choose parseProjectFile
            |> Array.toList
            |> List.distinctBy (fun p -> p.Name)
        with _ ->
            []
    
    /// Build dependency graph (order matters for compilation)
    type DepGraph = {
        AllProjects: ProjectInfo list
        ProjectsByName: Map<string, ProjectInfo>
        ProjectsByLayer: Map<BuildLayer, ProjectInfo list>
    }
    
    let buildGraph (projects: ProjectInfo list) : DepGraph =
        {
            AllProjects = projects
            ProjectsByName = 
                projects 
                |> List.map (fun p -> p.Name, p) 
                |> Map.ofList
            ProjectsByLayer =
                projects
                |> List.groupBy (fun p -> p.Layer)
                |> Map.ofList
        }
    
    /// Compute topological sort order for compilation
    /// Returns projects in dependency order (dependencies before dependents)
    let computeBuildOrder (graph: DepGraph) : Result<ProjectInfo list, string> =
        let rec visit 
            (visited: Set<string>) 
            (inStack: Set<string>) 
            (result: ProjectInfo list) 
            (proj: ProjectInfo) 
            : Result<ProjectInfo list, string> =
            
            if Set.contains proj.Name visited then
                Ok result
            elif Set.contains proj.Name inStack then
                Error $"Circular dependency detected involving {proj.Name}"
            else
                let newInStack = Set.add proj.Name inStack
                
                // Visit all dependencies first
                let depResults =
                    proj.Dependencies
                    |> List.fold (fun acc depName ->
                        match acc with
                        | Error e -> Error e
                        | Ok (visited', result') ->
                            match Map.tryFind depName graph.ProjectsByName with
                            | Some depProj ->
                                match visit visited' newInStack result' depProj with
                                | Ok newResult -> Ok (Set.add depProj.Name visited', newResult)
                                | Error e -> Error e
                            | None -> 
                                // Dependency not in workspace (external), skip
                                Ok (visited', result')
                    ) (Ok (visited, result))
                
                match depResults with
                | Ok (newVisited, newResult) ->
                    Ok (newResult @ [proj])
                | Error e -> Error e
        
        let mutable visited = Set.empty
        let mutable allResults = []
        
        let rec visitAll (projects: ProjectInfo list) : Result<ProjectInfo list, string> =
            match projects with
            | [] -> Ok allResults
            | proj :: rest ->
                if Set.contains proj.Name visited then
                    visitAll rest
                else
                    match visit visited Set.empty [] proj with
                    | Ok result ->
                        visited <- Set.add proj.Name visited
                        allResults <- result
                        visitAll rest
                    | Error e -> Error e
        
        visitAll graph.AllProjects
    
    /// Get projects that must be built before a target component
    let getDependencies (graph: DepGraph) (component: Component) : ProjectInfo list =
        let componentName = 
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
        
        match Map.tryFind componentName graph.ProjectsByName with
        | Some proj -> proj.Dependencies |> List.choose (fun depName -> Map.tryFind depName graph.ProjectsByName)
        | None -> []
    
    /// Get projects in a layer
    let getLayer (graph: DepGraph) (layer: BuildLayer) : ProjectInfo list =
        match Map.tryFind layer graph.ProjectsByLayer with
        | Some projs -> projs
        | None -> []
    
    /// Get all test projects
    let getTestProjects (graph: DepGraph) : ProjectInfo list =
        graph.AllProjects |> List.filter (fun p -> p.IsTestProject)
    
    /// Pretty-print dependency graph for debugging
    let printGraph (graph: DepGraph) : unit =
        printfn "Dependency Graph (%d projects):" graph.AllProjects.Length
        graph.AllProjects
        |> List.groupBy (fun p -> p.Layer)
        |> List.iter (fun (layer, projects) ->
            printfn "\n%A Layer:" layer
            projects |> List.iter (fun p ->
                let deps = match p.Dependencies with [] -> "none" | ds -> String.concat ", " ds
                printfn "  %s → [%s]" p.Name deps
            )
        )
