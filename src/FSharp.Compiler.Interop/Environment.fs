// Copyright (c) Sascha Dewald. See https://github.com/fsharp-wasm/fsharp

namespace FSharp.Compiler.Interop

/// Nullability-safe wrappers for System.Environment operations
module Environment =
    /// Gets an environment variable, returning None if not found
    let getEnvironmentVariable (variable: string): string option =
        if String.isNullOrEmpty variable then
            None
        else
            let result = System.Environment.GetEnvironmentVariable variable
            if String.isNullOrEmpty result then None else Some result

    /// Sets an environment variable
    let setEnvironmentVariable (variable: string) (value: string | null): unit =
        if not (String.isNullOrEmpty variable) then
            try
                System.Environment.SetEnvironmentVariable(variable, value)
            with _ -> ()

    /// The newline string for the current platform
    let newLine: string = System.Environment.NewLine

    /// Gets the number of processor cores
    let processorCount: int =
        try
            System.Environment.ProcessorCount
        with _ -> 1

    /// Gets the current user name
    let userName: string =
        try
            System.Environment.UserName
        with _ -> ""

    /// Gets the machine name
    let machineName: string =
        try
            System.Environment.MachineName
        with _ -> ""

    /// Exits the current process with exit code
    let exit (exitCode: int): unit =
        System.Environment.Exit exitCode

    /// Gets the OS version
    let osVersion: string =
        try
            System.Environment.OSVersion.ToString()
        with _ -> ""
