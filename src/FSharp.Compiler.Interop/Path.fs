// Copyright (c) Sascha Dewald. See https://github.com/fsharp-wasm/fsharp

namespace FSharp.Compiler.Interop

/// Nullability-safe wrappers for System.IO.Path operations
module Path =
    /// Directory separator character for the current platform
    let directorySeparatorChar: char = System.IO.Path.DirectorySeparatorChar

    /// Alternate directory separator character
    let altDirectorySeparatorChar: char = System.IO.Path.AltDirectorySeparatorChar

    /// Path separator character (for PATH environment variable)
    let pathSeparator: char = System.IO.Path.PathSeparator

    /// Volume separator character
    let volumeSeparatorChar: char = System.IO.Path.VolumeSeparatorChar

    /// Combines path components into a single path
    let combine (paths: string[]): string =
        if Array.isEmpty paths then ""
        else System.IO.Path.Combine(paths)

    /// Combines two path components
    let combine2 (path1: string) (path2: string): string =
        System.IO.Path.Combine(path1, path2)

    /// Combines three path components
    let combine3 (path1: string) (path2: string) (path3: string): string =
        System.IO.Path.Combine(path1, path2, path3)

    /// Gets the directory name of a path, returning None if null or empty
    let getDirectoryName (path: string): string option =
        if String.isNullOrEmpty path then
            None
        else
            let result = System.IO.Path.GetDirectoryName path
            if String.isNullOrEmpty result then None else Some result

    /// Gets the file name including extension, returning None if null or empty
    let getFileName (path: string): string option =
        if String.isNullOrEmpty path then
            None
        else
            let result = System.IO.Path.GetFileName path
            if String.isNullOrEmpty result then None else Some result

    /// Gets the file name without extension, returning None if null or empty
    let getFileNameWithoutExtension (path: string): string option =
        if String.isNullOrEmpty path then
            None
        else
            try
                let result = System.IO.Path.GetFileNameWithoutExtension path
                if String.isNullOrEmpty result then None else Some result
            with _ -> None

    /// Gets the file extension, including the dot, returning None if null or empty
    let getExtension (path: string): string option =
        if String.isNullOrEmpty path then
            None
        else
            let result = System.IO.Path.GetExtension path
            if String.isNullOrEmpty result then None else Some result

    /// Gets the absolute path for a relative path
    let getFullPath (path: string): string =
        if String.isNullOrEmpty path then ""
        else System.IO.Path.GetFullPath path

    /// Gets the temporary directory path
    let getTempPath (): string =
        System.IO.Path.GetTempPath()

    /// Gets a temporary file name
    let getTempFileName (): string =
        System.IO.Path.GetTempFileName()

    /// Checks if path has an extension
    let hasExtension (path: string): bool =
        if String.isNullOrEmpty path then false
        else System.IO.Path.HasExtension path

    /// Checks if a path is rooted (absolute)
    let isPathRooted (path: string): bool =
        if String.isNullOrEmpty path then false
        else System.IO.Path.IsPathRooted path
