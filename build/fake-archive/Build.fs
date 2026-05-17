module Build

open Fake.Core
open Config
open Targets

[<EntryPoint>]
let main argv =
    try
        // Parse and validate command-line arguments
        let opts =
            parseArgs argv
            |> function
                | Ok o -> o
                | Error msg ->
                    Trace.error msg
                    exit 1

        Trace.logf "Building with options: %A" opts

        // Initialize targets and run default target
        initTargets opts
        Target.runOrDefault "Build"
        0
    with
    | Failure msg ->
        Trace.error msg
        1
    | ex ->
        Trace.error (sprintf "Unexpected error: %s" ex.Message)
        1