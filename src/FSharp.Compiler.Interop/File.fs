// Copyright (c) Sascha Dewald. See https://github.com/fsharp-wasm/fsharp

namespace FSharp.Compiler.Interop

/// Nullability-safe wrappers for System.IO.File operations
module File =
    /// Checks if a file exists
    let exists (path: string): bool =
        if String.isNullOrEmpty path then false
        else
            try
                System.IO.File.Exists path
            with _ -> false

    /// Reads all text from a file using UTF8
    let readAllText (path: string): string =
        if String.isNullOrEmpty path || not (exists path) then
            ""
        else
            try
                System.IO.File.ReadAllText path
            with _ -> ""

    /// Reads all text from a file with specified encoding
    let readAllTextEncoding (path: string) (encoding: System.Text.Encoding): string =
        if String.isNullOrEmpty path || not (exists path) then
            ""
        else
            try
                System.IO.File.ReadAllText(path, encoding)
            with _ -> ""

    /// Reads all bytes from a file
    let readAllBytes (path: string): byte[] =
        if String.isNullOrEmpty path || not (exists path) then
            [||]
        else
            try
                System.IO.File.ReadAllBytes path
            with _ -> [||]

    /// Reads all lines from a file
    let readAllLines (path: string): string[] =
        if String.isNullOrEmpty path || not (exists path) then
            [||]
        else
            try
                System.IO.File.ReadAllLines path
            with _ -> [||]

    /// Reads all lines from a file with specified encoding
    let readAllLinesEncoding (path: string) (encoding: System.Text.Encoding): string[] =
        if String.isNullOrEmpty path || not (exists path) then
            [||]
        else
            try
                System.IO.File.ReadAllLines(path, encoding)
            with _ -> [||]

    /// Writes all text to a file (overwrites if exists)
    let writeAllText (path: string) (contents: string): unit =
        if not (String.isNullOrEmpty path) then
            try
                System.IO.File.WriteAllText(path, contents)
            with _ -> ()

    /// Writes all text to a file with specified encoding
    let writeAllTextEncoding (path: string) (contents: string) (encoding: System.Text.Encoding): unit =
        if not (String.isNullOrEmpty path) then
            try
                System.IO.File.WriteAllText(path, contents, encoding)
            with _ -> ()

    /// Writes all bytes to a file (overwrites if exists)
    let writeAllBytes (path: string) (bytes: byte[]): unit =
        if not (String.isNullOrEmpty path) && not (isNull bytes) then
            try
                System.IO.File.WriteAllBytes(path, bytes)
            with _ -> ()

    /// Writes all lines to a file
    let writeAllLines (path: string) (lines: string[]): unit =
        if not (String.isNullOrEmpty path) && not (isNull lines) then
            try
                System.IO.File.WriteAllLines(path, lines)
            with _ -> ()

    /// Appends text to a file
    let appendAllText (path: string) (contents: string): unit =
        if not (String.isNullOrEmpty path) && not (String.isNullOrEmpty contents) then
            try
                System.IO.File.AppendAllText(path, contents)
            with _ -> ()

    /// Appends text to a file with specified encoding
    let appendAllTextEncoding (path: string) (contents: string) (encoding: System.Text.Encoding): unit =
        if not (String.isNullOrEmpty path) && not (String.isNullOrEmpty contents) then
            try
                System.IO.File.AppendAllText(path, contents, encoding)
            with _ -> ()

    /// Appends lines to a file
    let appendAllLines (path: string) (lines: string[]): unit =
        if not (String.isNullOrEmpty path) && not (isNull lines) then
            try
                System.IO.File.AppendAllLines(path, lines)
            with _ -> ()

    /// Deletes a file
    let delete (path: string): unit =
        if not (String.isNullOrEmpty path) && exists path then
            try
                System.IO.File.Delete path
            with _ -> ()

    /// Copies a file
    let copy (sourceFileName: string) (destFileName: string): unit =
        if not (String.isNullOrEmpty sourceFileName) && not (String.isNullOrEmpty destFileName) && exists sourceFileName then
            try
                System.IO.File.Copy(sourceFileName, destFileName, true)
            with _ -> ()

    /// Copies a file with overwrite option
    let copyOverwrite (sourceFileName: string) (destFileName: string) (overwrite: bool): unit =
        if not (String.isNullOrEmpty sourceFileName) && not (String.isNullOrEmpty destFileName) && exists sourceFileName then
            try
                System.IO.File.Copy(sourceFileName, destFileName, overwrite)
            with _ -> ()

    /// Moves a file (safe version that handles potential overwrites)
    let move (sourceFileName: string) (destFileName: string): unit =
        if not (String.isNullOrEmpty sourceFileName) && not (String.isNullOrEmpty destFileName) && exists sourceFileName then
            try
                // Delete destination if it exists to simulate overwrite
                if exists destFileName then
                    try
                        delete destFileName
                    with _ -> ()
                System.IO.File.Move(sourceFileName, destFileName)
            with _ -> ()

    /// Opens a file for reading
    let openRead (path: string): System.IO.FileStream option =
        if String.isNullOrEmpty path || not (exists path) then
            None
        else
            try
                Some(System.IO.File.OpenRead path)
            with _ -> None

    /// Opens a file for writing
    let openWrite (path: string): System.IO.FileStream option =
        if String.isNullOrEmpty path then
            None
        else
            try
                Some(System.IO.File.OpenWrite path)
            with _ -> None

    /// Gets the last write time (UTC) of a file
    let getLastWriteTimeUtc (path: string): System.DateTime =
        if String.isNullOrEmpty path || not (exists path) then
            System.DateTime.MinValue
        else
            try
                System.IO.File.GetLastWriteTimeUtc path
            with _ -> System.DateTime.MinValue

    /// Gets the last access time (UTC) of a file
    let getLastAccessTimeUtc (path: string): System.DateTime =
        if String.isNullOrEmpty path || not (exists path) then
            System.DateTime.MinValue
        else
            try
                System.IO.File.GetLastAccessTimeUtc path
            with _ -> System.DateTime.MinValue

    /// Gets the creation time (UTC) of a file
    let getCreationTimeUtc (path: string): System.DateTime =
        if String.isNullOrEmpty path || not (exists path) then
            System.DateTime.MinValue
        else
            try
                System.IO.File.GetCreationTimeUtc path
            with _ -> System.DateTime.MinValue

    /// Creates or truncates a file and returns a FileStream for writing
    let create (path: string): System.IO.FileStream =
        if String.isNullOrEmpty path then
            System.IO.File.Create ""  // Will throw, which is appropriate
        else
            System.IO.File.Create path
