// Copyright (c) Sascha Dewald. See https://github.com/fsharp-wasm/fsharp

namespace FSharp.Compiler.Interop

/// Nullability-safe wrappers for System.Text.RegularExpressions operations
module Regex =
    open System
    open System.Text.RegularExpressions

    /// Creates a Regex from a pattern
    let create (pattern: string): Regex option =
        if String.isNullOrEmpty pattern then None
        else
            try
                Some(new Regex(pattern))
            with _ -> None

    /// Creates a Regex from a pattern with options
    let createWithOptions (pattern: string) (options: RegexOptions): Regex option =
        if String.isNullOrEmpty pattern then None
        else
            try
                Some(new Regex(pattern, options))
            with _ -> None

    /// Tests if a pattern matches the input string
    let isMatch (pattern: string) (input: string): bool =
        if String.isNullOrEmpty pattern || isNull input then false
        else
            try
                Regex.IsMatch(input, pattern)
            with _ -> false

    /// Tests if a pattern matches with RegexOptions
    let isMatchWithOptions (pattern: string) (input: string) (options: RegexOptions): bool =
        if String.isNullOrEmpty pattern || isNull input then false
        else
            try
                Regex.IsMatch(input, pattern, options)
            with _ -> false

    /// Finds the first match in the input string
    let match_ (pattern: string) (input: string): Match option =
        if String.isNullOrEmpty pattern || isNull input then None
        else
            try
                let m = Regex.Match(input, pattern)
                if m.Success then Some m else None
            with _ -> None

    /// Finds all matches in the input string
    let matches (pattern: string) (input: string): MatchCollection option =
        if String.isNullOrEmpty pattern || isNull input then None
        else
            try
                Some(Regex.Matches(input, pattern))
            with _ -> None

    /// Replaces matches of a pattern with a replacement string
    let replace (pattern: string) (replacement: string) (input: string): string =
        if String.isNullOrEmpty pattern || isNull input then input
        else
            try
                Regex.Replace(input, pattern, replacement)
            with _ -> input

    /// Replaces matches with options
    let replaceWithOptions (pattern: string) (replacement: string) (input: string) (options: RegexOptions): string =
        if String.isNullOrEmpty pattern || isNull input then input
        else
            try
                Regex.Replace(input, pattern, replacement, options)
            with _ -> input

    /// Splits a string by a pattern
    let split (pattern: string) (input: string): string[] =
        if String.isNullOrEmpty pattern || isNull input then [||]
        else
            try
                Regex.Split(input, pattern)
            with _ -> [||]

    /// Checks if a Regex instance is null
    let isNullRegex (regex: Regex): bool =
        isNull regex

    /// Finds first match using a Regex instance
    let matchRegex (regex: Regex) (input: string): Match option =
        if isNull regex || isNull input then None
        else
            try
                let m = regex.Match(input)
                if m.Success then Some m else None
            with _ -> None

    /// Finds all matches using a Regex instance
    let matchesRegex (regex: Regex) (input: string): MatchCollection option =
        if isNull regex || isNull input then None
        else
            try
                Some(regex.Matches(input))
            with _ -> None

    /// Replaces matches using a Regex instance
    let replaceRegex (regex: Regex) (replacement: string) (input: string): string =
        if isNull regex || isNull input then input
        else
            try
                regex.Replace(input, replacement)
            with _ -> input

    /// Splits a string using a Regex instance
    let splitRegex (regex: Regex) (input: string): string[] =
        if isNull regex || isNull input then [||]
        else
            try
                regex.Split(input)
            with _ -> [||]

    /// Gets the pattern from a Regex
    let getPattern (regex: Regex): string option =
        if isNull regex then None
        else
            try
                let pattern = regex.ToString()
                if String.isNullOrEmpty pattern then None else Some pattern
            with _ -> None

    /// Gets Match index
    let getMatchIndex (m: Match): int option =
        if isNull m then None
        else
            try
                Some(m.Index)
            with _ -> None

    /// Gets Match length
    let getMatchLength (m: Match): int option =
        if isNull m then None
        else
            try
                Some(m.Length)
            with _ -> None

    /// Gets Match value
    let getMatchValue (m: Match): string option =
        if isNull m then None
        else
            try
                Some(m.Value)
            with _ -> None

    /// Gets Match groups
    let getMatchGroups (m: Match): Group[] option =
        if isNull m then None
        else
            try
                Some(m.Groups |> Array.ofSeq)
            with _ -> None

    /// Gets group value by index
    let getGroupValue (m: Match) (index: int): string option =
        if isNull m || index < 0 then None
        else
            try
                let grp = m.Groups.[index]
                if isNull grp || not grp.Success then None else Some grp.Value
            with _ -> None
