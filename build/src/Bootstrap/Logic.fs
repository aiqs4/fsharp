/// Bootstrap compiler build logic
/// Builds the Proto compiler using the SDK, which is then used for self-compilation

namespace FSharpBuild.Bootstrap

open System
open System.IO
open System.Diagnostics
open FSharpBuild.Types
open FSharpBuild.Paths
open FSharpBuild.Config

module Logic =
    
    /// Check if bootstrap is already built and valid
    let isBootstrapValid (ctx: BuildContext) : bool =
        let fscPath = Paths.bootstrapFsc ctx
        if not (File.Exists fscPath) then 
            false
        else
            // Check if bootstrap is recent (within 1 hour of artifact time)
            let bootstrapAge = DateTime.Now - File.GetLastWriteTime fscPath
            bootstrapAge < TimeSpan.FromHours 1
    
    /// Cache bootstrap across configurations (optional optimization)
    /// Set FSHARP_BUILD_CACHE_BOOTSTRAP=true to enable
    let isCacheBootstrapEnabled () : bool =
        Environment.GetEnvironmentVariable("FSHARP_BUILD_CACHE_BOOTSTRAP")
        |> Option.ofObj
        |> Option.map (fun s -> s.ToLower() = "true")
        |> Option.defaultValue false
    
    /// Check if bootstrap should be skipped
    /// Returns: (shouldSkip, reason)
    let shouldSkipBootstrap (ctx: BuildContext) : bool * string =
        if not (isCacheBootstrapEnabled ()) then
            false, "caching disabled"
        else if isBootstrapValid ctx then
            true, "bootstrap cache valid"
        else
            false, "bootstrap cache invalid or missing"
    
    /// Run bootstrap build via dotnet CLI
    /// Uses the SDK's own compiler to build fsc/fsi
    let buildBootstrap (ctx: BuildContext) : Result<TimeSpan, string> =
        try
            Paths.ensureDirectories ctx
            
            let startTime = DateTime.Now
            let proto = ctx.Config
            
            // The bootstrap passes specific configuration to Proto projects
            let args = [
                "-c"; proto.Configuration.ToString()
                "--no-restore"
                $"--output:{Paths.bootstrapDir ctx}"
            ]
            
            let protoProj = Path.Combine(ctx.RootPath, "proto.proj")
            if not (File.Exists protoProj) then
                Error "proto.proj not found"
            else
                let psi = ProcessStartInfo(
                    FileName = "dotnet",
                    Arguments = String.concat " " (["build"; protoProj] @ args),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                )
                psi.EnvironmentVariables["FSHARP_BUILD_BOOTSTRAP"] <- "true"
                
                use proc = Process.Start(psi)
                let output = proc.StandardOutput.ReadToEnd()
                let errors = proc.StandardError.ReadToEnd()
                proc.WaitForExit()
                
                let duration = DateTime.Now - startTime
                
                if proc.ExitCode <> 0 then
                    Error $"Bootstrap build failed (exit {proc.ExitCode}):\n{errors}"
                else
                    Ok duration
        
        with ex ->
            Error $"Bootstrap build exception: {ex.Message}"
    
    /// Clean bootstrap artifacts
    let cleanBootstrap (ctx: BuildContext) : unit =
        Paths.clean ctx CleanMode.Bootstrap
    
    /// Validate bootstrap was built correctly
    let validateBootstrap (ctx: BuildContext) : Result<unit, string> =
        let fscPath = Paths.bootstrapFsc ctx
        
        if not (File.Exists fscPath) then
            Error $"Bootstrap fsc not found at {fscPath}"
        else if not (File.Exists (Path.Combine(Paths.bootstrapDir ctx, "fsi.dll"))) then
            Error "Bootstrap fsi.dll not found"
        else
            Ok ()
    
    /// Get bootstrap environment variables for downstream builds
    /// These tell the build system to use the bootstrap compiler
    let getBootstrapEnv (ctx: BuildContext) : (string * string) list =
        let bootstrapDir = Paths.bootstrapDir ctx
        [
            "FSHARP_BUILD_BOOTSTRAP"; bootstrapDir
            "FSharpTargetsPath"; Path.Combine(bootstrapDir, "fsc.dll")
            "FSharpBinDir"; bootstrapDir
        ]

module Status =
    
    /// Report bootstrap status
    type BootstrapStatus =
        | New            // Never built
        | Valid          // Built and valid
        | Stale          // Built but needs rebuild
        | Invalid        // Build failed
    
    let getStatus (ctx: BuildContext) : BootstrapStatus =
        let dir = Paths.bootstrapDir ctx
        
        if not (Directory.Exists dir) then
            New
        else if Logic.isBootstrapValid ctx then
            Valid
        else
            Stale
    
    let statusString : BootstrapStatus -> string = function
        | New -> "BOOTSTRAP NEEDED"
        | Valid -> "bootstrap ✓"
        | Stale -> "BOOTSTRAP OUTDATED"
        | Invalid -> "BOOTSTRAP FAILED"
    
    /// Report bootstrap summary
    let report (ctx: BuildContext) : string =
        let status = getStatus ctx
        let fscPath = Paths.bootstrapFsc ctx
        let timestamp = 
            if File.Exists fscPath then
                File.GetLastWriteTime(fscPath).ToString("yyyy-MM-dd HH:mm:ss")
            else
                "never built"
        
        $"Bootstrap {statusString status} ({timestamp})"
