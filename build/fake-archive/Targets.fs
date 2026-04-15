module Targets

open Fake.Core
open Config
open Bootstrap
open Compiler
open Artifacts

/// Initialize all FAKE targets and dependencies
let initTargets (opts: BuildOptions) =
    // Clean artifacts
    Target.create "Clean" (fun _ ->
        Bootstrap.clean()
        cleanBin()
        Trace.log "Clean complete"
    )

    // Bootstrap only
    Target.create "BuildBootstrap" (fun _ ->
        Bootstrap.run opts
    )

    // Build compiler
    Target.create "BuildCompiler" (fun _ ->
        Compiler.build opts
    )

    // Validate compiler artifacts
    Target.create "Verify" (fun _ ->
        Compiler.verify opts
        validateReleaseArtifacts opts
    )

    // Full build: bootstrap + compile + verify
    Target.create "Build" (fun _ ->
        Trace.log "=== F# Compiler Build ==="
        Bootstrap.run opts
        Compiler.build opts
        Compiler.verify opts
    )

    // Rebuild from scratch
    Target.create "Rebuild" (fun _ ->
        Bootstrap.clean()
        Target.run "Build" []
    )

    // Define dependencies
    "BuildCompiler" |> Target.dependsOn "BuildBootstrap"
    "Verify"       |> Target.dependsOn "BuildCompiler"
    "Build"        |> Target.dependsOn "Verify"