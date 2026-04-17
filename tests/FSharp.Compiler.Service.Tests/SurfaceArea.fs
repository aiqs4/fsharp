// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.Service.Tests.SurfaceArea

open System.IO
open System.Reflection
open Xunit

type SurfaceAreaTest() =
    // This relies on a set of baselines to update the baseline set an environment variable before running the tests, then on failure the baselines will be updated
    // Handled by SurfaceArea.verify
    //
    // CMD:
    //    set TEST_UPDATE_BSL=1
    // PowerShell:
    //    $env:TEST_UPDATE_BSL=1
    // Linux/macOS:
    //    export TEST_UPDATE_BSL=1

    [<Fact>]
    member _.VerifySurfaceAreaFSharpCompilerService() =
        // System.Environment.SetEnvironmentVariable("TEST_UPDATE_BSL", "1")

        let assembly =
            let path = Path.Combine(Path.GetDirectoryName(typeof<int list>.Assembly.Location), "FSharp.Compiler.Service.dll")
            Assembly.LoadFrom path

        // TODO: maybe obsolete and we can use upstream
        // Try baseline files in order (net first, then netstandard fallback)
        let tryFindBaseline (patterns: string list) =
            patterns
            |> List.tryFind (fun pattern -> File.Exists(Path.Combine(__SOURCE_DIRECTORY__, pattern)))
            |> Option.map (fun pattern -> Path.Combine(__SOURCE_DIRECTORY__, pattern), pattern)

        let possibleBaselines = [
            "FSharp.Compiler.Service.SurfaceArea.net10.0.bsl"      // Prefer net10
            "FSharp.Compiler.Service.SurfaceArea.netstandard21.bsl" // Fallback to netstandard21
            "FSharp.Compiler.Service.SurfaceArea.netstandard20.bsl" // Fallback to netstandard20
        ]

        let (baseline, _) = 
            match tryFindBaseline possibleBaselines with
            | Some result -> result
            | None -> 
                // If no baseline found, use net10 (will fail with proper error message)
                (Path.Combine(__SOURCE_DIRECTORY__, "FSharp.Compiler.Service.SurfaceArea.net10.0.bsl"), 
                 "FSharp.Compiler.Service.SurfaceArea.net10.0.bsl")

        let outFileName = Path.Combine(__SOURCE_DIRECTORY__, "FSharp.Compiler.Service.SurfaceArea.net10.0.out")
        FSharp.Test.SurfaceArea.verify assembly baseline outFileName
