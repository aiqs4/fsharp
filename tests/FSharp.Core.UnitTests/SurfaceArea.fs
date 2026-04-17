// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Core.UnitTests.Portable.SurfaceArea

open Xunit
open System.IO
open FSharp.Test

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
    member this.VerifySurfaceAreaFSharpCore () : unit =
        let assembly = typeof<int list>.Assembly

        // Determine platform by trying baseline files in order (net first, then netstandard fallback)
        let flavor =
#if DEBUG
            "debug"
#else
            "release"
#endif

        let tryFindBaseline (dir: string) (patterns: string list) =
            patterns
            |> List.tryFind (fun pattern -> File.Exists(Path.Combine(dir, pattern)))
            |> Option.map (fun pattern -> Path.Combine(dir, pattern), pattern)

        let dir = __SOURCE_DIRECTORY__
        let possibleBaselines = [
            $"FSharp.Core.SurfaceArea.net10.0.{flavor}.bsl"      // Prefer net10
            $"FSharp.Core.SurfaceArea.netstandard21.{flavor}.bsl" // Fallback to netstandard21
            $"FSharp.Core.SurfaceArea.netstandard20.{flavor}.bsl" // Fallback to netstandard20
        ]

        let (baseline, _) =
            match tryFindBaseline dir possibleBaselines with
            | Some result -> result
            | None ->
                // If no baseline found, use net10 (will fail with proper error message)
                (Path.Combine(dir, $"FSharp.Core.SurfaceArea.net10.0.{flavor}.bsl"),
                 $"FSharp.Core.SurfaceArea.net10.0.{flavor}.bsl")

        let outFileName = Path.Combine(Path.GetDirectoryName(assembly.Location), $"FSharp.Core.SurfaceArea.net10.0.{flavor}.out")
        SurfaceArea.verify assembly baseline outFileName
