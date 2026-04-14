// Copyright (c) Sascha Dewald. See https://github.com/fsharp-wasm/fsharp

namespace FSharp.Compiler.Interop

/// Nullability-safe wrappers for System.String operations
module String =
    /// Checks if a string is null or empty
    let isNullOrEmpty (value: string | null): bool =
        System.String.IsNullOrEmpty value

    /// Checks if a string is null, empty, or whitespace
    let isNullOrWhiteSpace (value: string | null): bool =
        System.String.IsNullOrWhiteSpace value

    /// Concatenates multiple strings (no separator)
    let concat (values: string[]): string =
        System.String.Concat values

    /// Joins multiple strings with a separator
    let join (separator: string) (values: string[]): string =
        System.String.Join(separator, values)

    /// Formats a string with arguments
    let format (fmt: string) (args: obj[]): string =
        System.String.Format(fmt, args)

    /// Compares two strings using the specified comparison option
    let compare (strA: string) (strB: string) (comparison: System.StringComparison): int =
        System.String.Compare(strA, strB, comparison)

    /// Equals check with specified comparison
    let equals (a: string) (b: string) (comparison: System.StringComparison): bool =
        System.String.Equals(a, b, comparison)

    /// Empty string constant
    let empty: string = System.String.Empty

    /// Get the length of a string
    let length (str: string): int =
        if isNull str then 0 else str.Length

    /// Get substring starting at position
    let substring (startIndex: int) (str: string): string option =
        if isNull str || startIndex < 0 || startIndex >= str.Length then
            None
        else
            try
                Some(str.Substring(startIndex))
            with _ -> None

    /// Get substring with specific length
    let substringSafe (startIndex: int) (length: int) (str: string): string option =
        if isNull str || startIndex < 0 || length < 0 || startIndex + length > str.Length then
            None
        else
            try
                Some(str.Substring(startIndex, length))
            with _ -> None

    /// Split a string by separators
    let split (separators: char[]) (str: string): string[] =
        if isNull str then [||]
        else str.Split(separators)

    /// Split with string split options
    let splitOptions (separators: char[]) (options: System.StringSplitOptions) (str: string): string[] =
        if isNull str then [||]
        else str.Split(separators, options)

    /// Trim a string of whitespace
    let trim (str: string): string =
        if isNull str then str
        else str.Trim()

    /// Trim specific characters
    let trimChars (trimChars: char[]) (str: string): string =
        if isNull str then str
        else str.Trim(trimChars)

    /// Convert to lowercase
    let toLower (str: string): string =
        if isNull str then str
        else str.ToLower()

    /// Convert to uppercase
    let toUpper (str: string): string =
        if isNull str then str
        else str.ToUpper()

    /// Check if string starts with prefix
    let startsWith (prefix: string) (str: string): bool =
        if isNull str || isNull prefix then false
        else str.StartsWith(prefix)

    /// Check if string ends with suffix
    let endsWith (suffix: string) (str: string): bool =
        if isNull str || isNull suffix then false
        else str.EndsWith(suffix)

    /// Check if string contains substring
    let contains (substring: string) (str: string): bool =
        if isNull str || isNull substring then false
        else str.Contains(substring)

    /// Replace substring with replacement
    let replace (oldValue: string) (newValue: string) (str: string): string =
        if isNull str then str
        else str.Replace(oldValue, newValue)
