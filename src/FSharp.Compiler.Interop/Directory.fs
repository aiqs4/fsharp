// Copyright (c) Sascha Dewald. See https://github.com/fsharp-wasm/fsharp

namespace FSharp.Compiler.Interop

/// Nullability-safe wrappers for System.IO.Directory operations
module Directory =
    /// Checks if a directory exists
    let exists (path: string): bool =
        if String.isNullOrEmpty path then false
        else
            try
                System.IO.Directory.Exists path
            with _ -> false

    /// Creates a directory
    let create (path: string): unit =
        if not (String.isNullOrEmpty path) then
            try
                System.IO.Directory.CreateDirectory path |> ignore
            with _ -> ()

    /// Gets the current working directory
    let getCurrentDirectory (): string =
        try
            System.IO.Directory.GetCurrentDirectory()
        with _ -> ""

    /// Sets the current working directory
    let setCurrentDirectory (path: string): unit =
        if not (String.isNullOrEmpty path) then
            try
                System.IO.Directory.SetCurrentDirectory path
            with _ -> ()

    /// Gets all files in a directory (non-recursive)
    let getFiles (path: string): string[] =
        if String.isNullOrEmpty path || not (exists path) then
            [||]
        else
            try
                System.IO.Directory.GetFiles path
            with _ -> [||]

    /// Gets all files matching a pattern
    let getFilesPattern (path: string) (pattern: string): string[] =
        if String.isNullOrEmpty path || String.isNullOrEmpty pattern || not (exists path) then
            [||]
        else
            try
                System.IO.Directory.GetFiles(path, pattern)
            with _ -> [||]

    /// Gets all directories in a directory (non-recursive)
    let getDirectories (path: string): string[] =
        if String.isNullOrEmpty path || not (exists path) then
            [||]
        else
            try
                System.IO.Directory.GetDirectories path
            with _ -> [||]

    /// Gets all directories matching a pattern
    let getDirectoriesPattern (path: string) (pattern: string): string[] =
        if String.isNullOrEmpty path || String.isNullOrEmpty pattern || not (exists path) then
            [||]
        else
            try
                System.IO.Directory.GetDirectories(path, pattern)
            with _ -> [||]

    /// Deletes a directory (non-recursive by default)
    let delete (path: string): unit =
        if not (String.isNullOrEmpty path) && exists path then
            try
                System.IO.Directory.Delete path
            with _ -> ()

    /// Deletes a directory with option for recursive deletion
    let deleteRecursive (path: string) (recursive: bool): unit =
        if not (String.isNullOrEmpty path) && exists path then
            try
                System.IO.Directory.Delete(path, recursive)
            with _ -> ()
