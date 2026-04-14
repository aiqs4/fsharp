// Copyright (c) Sascha Dewald. See https://github.com/fsharp-wasm/fsharp

namespace FSharp.Compiler.Interop

/// Nullability-safe wrappers for System.Text.Encoding operations
module Encoding =
    /// UTF8 encoding
    let utf8: System.Text.Encoding = System.Text.Encoding.UTF8

    /// Unicode (UTF16) encoding
    let unicode: System.Text.Encoding = System.Text.Encoding.Unicode

    /// ASCII encoding
    let ascii: System.Text.Encoding = System.Text.Encoding.ASCII

    /// Default encoding (platform specific)
    let default_: System.Text.Encoding = System.Text.Encoding.Default

    /// Gets the string representation of bytes using UTF8
    let getString (bytes: byte[]): string =
        if isNull bytes || Array.isEmpty bytes then ""
        else
            try
                utf8.GetString bytes
            with _ -> ""

    /// Gets the string representation of bytes with offset and count using UTF8
    let getStringRange (bytes: byte[]) (index: int) (count: int): string =
        if isNull bytes || Array.isEmpty bytes || index < 0 || count < 0 || index + count > Array.length bytes then
            ""
        else
            try
                utf8.GetString(bytes, index, count)
            with _ -> ""

    /// Gets the string representation of bytes using specified encoding
    let getStringEncoding (encoding: System.Text.Encoding) (bytes: byte[]): string =
        if isNull encoding || isNull bytes || Array.isEmpty bytes then ""
        else
            try
                encoding.GetString bytes
            with _ -> ""

    /// Gets the string representation of bytes with offset and count using specified encoding
    let getStringRangeEncoding (encoding: System.Text.Encoding) (bytes: byte[]) (index: int) (count: int): string =
        if isNull encoding || isNull bytes || Array.isEmpty bytes || index < 0 || count < 0 || index + count > Array.length bytes then
            ""
        else
            try
                encoding.GetString(bytes, index, count)
            with _ -> ""

    /// Gets the byte representation of a string using UTF8
    let getBytes (str: string): byte[] =
        if String.isNullOrEmpty str then [||]
        else
            try
                utf8.GetBytes str
            with _ -> [||]

    /// Gets the byte representation of a string using specified encoding
    let getBytesEncoding (encoding: System.Text.Encoding) (str: string): byte[] =
        if isNull encoding || String.isNullOrEmpty str then [||]
        else
            try
                encoding.GetBytes str
            with _ -> [||]

    /// Gets the byte representation of a string array with offset and count using UTF8
    let getBytesRange (str: string) (charIndex: int) (charCount: int): byte[] =
        if String.isNullOrEmpty str || charIndex < 0 || charCount < 0 || charIndex + charCount > String.length str then
            [||]
        else
            try
                utf8.GetBytes(str, charIndex, charCount)
            with _ -> [||]

    /// Gets the byte count for a string using UTF8
    let getByteCount (str: string): int =
        if String.isNullOrEmpty str then 0
        else
            try
                utf8.GetByteCount str
            with _ -> 0

    /// Gets the byte count for a string using specified encoding
    let getByteCountEncoding (encoding: System.Text.Encoding) (str: string): int =
        if isNull encoding || String.isNullOrEmpty str then 0
        else
            try
                encoding.GetByteCount str
            with _ -> 0

    /// Gets the char count for bytes using UTF8
    let getCharCount (bytes: byte[]): int =
        if isNull bytes || Array.isEmpty bytes then 0
        else
            try
                utf8.GetCharCount bytes
            with _ -> 0

    /// Gets the char count for bytes using specified encoding
    let getCharCountEncoding (encoding: System.Text.Encoding) (bytes: byte[]): int =
        if isNull encoding || isNull bytes || Array.isEmpty bytes then 0
        else
            try
                encoding.GetCharCount bytes
            with _ -> 0
