// Copyright (c) Sascha Dewald. See https://github.com/fsharp-wasm/fsharp

namespace FSharp.Compiler.Interop

/// Nullability-safe wrappers for System.Threading.Interlocked operations
module Interlocked =
    open System
    open System.Threading

    /// Atomically increments a 32-bit integer
    let increment (location: byref<int>): int =
        try
            System.Threading.Interlocked.Increment(&location)
        with _ -> location

    /// Atomically decrements a 32-bit integer
    let decrement (location: byref<int>): int =
        try
            System.Threading.Interlocked.Decrement(&location)
        with _ -> location

    /// Atomically adds a value to a 32-bit integer
    let add (location: byref<int>) (value: int): int =
        try
            System.Threading.Interlocked.Add(&location, value)
        with _ -> location + value

    /// Atomically exchanges two 32-bit integers
    let exchange (location: byref<int>) (value: int): int =
        try
            System.Threading.Interlocked.Exchange(&location, value)
        with _ -> value

    /// Atomically compares and exchanges two 32-bit integers
    let compareExchange (location: byref<int>) (value: int) (comparand: int): int =
        try
            System.Threading.Interlocked.CompareExchange(&location, value, comparand)
        with _ -> location

    /// Atomically exchanges two reference types
    let exchangeRef<'T when 'T: not struct> (location: byref<'T>) (value: 'T): 'T =
        try
            System.Threading.Interlocked.Exchange(&location, value)
        with _ -> value

    /// Atomically compares and exchanges two reference types
    let compareExchangeRef<'T when 'T: not struct> (location: byref<'T>) (value: 'T) (comparand: 'T): 'T =
        try
            System.Threading.Interlocked.CompareExchange(&location, value, comparand)
        with _ -> location
