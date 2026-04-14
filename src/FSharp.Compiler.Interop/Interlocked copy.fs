// Copyright (c) Sascha Dewald. See https://github.com/fsharp-wasm/fsharp

namespace FSharp.Compiler.Interop

/// Nullability-safe wrappers for System.Threading.Interlocked operations
module Interlocked =
    open System
    open System.Threading

    /// Atomically increments a 32-bit integer
    let increment (location: byref<int>): int =
        try
            Interlocked.Increment(&location)
        with _ -> location

    /// Atomically increments a 64-bit integer
    let incrementInt64 (location: byref<int64>): int64 =
        try
            Interlocked.Increment(&location)
        with _ -> location

    /// Atomically decrements a 32-bit integer
    let decrement (location: byref<int>): int =
        try
            Interlocked.Decrement(&location)
        with _ -> location

    /// Atomically decrements a 64-bit integer
    let decrementInt64 (location: byref<int64>): int64 =
        try
            Interlocked.Decrement(&location)
        with _ -> location

    /// Atomically adds a value to a 32-bit integer
    let add (location: byref<int>) (value: int): int =
        try
            Interlocked.Add(&location, value)
        with _ -> location + value

    /// Atomically adds a value to a 64-bit integer
    let addInt64 (location: byref<int64>) (value: int64): int64 =
        try
            Interlocked.Add(&location, value)
        with _ -> location + value

    /// Atomically exchanges two 32-bit integers
    let exchange (location: byref<int>) (value: int): int =
        try
            Interlocked.Exchange(&location, value)
        with _ -> value

    /// Atomically exchanges two 64-bit integers
    let exchangeInt64 (location: byref<int64>) (value: int64): int64 =
        try
            Interlocked.Exchange(&location, value)
        with _ -> value

    /// Atomically exchanges two reference types
    let exchangeRef<'T> (location: byref<'T>) (value: 'T): 'T when 'T: class =
        try
            Interlocked.Exchange(&location, value)
        with _ -> value

    /// Atomically compares and exchanges two 32-bit integers
    let compareExchange (location: byref<int>) (value: int) (comparand: int): int =
        try
            Interlocked.CompareExchange(&location, value, comparand)
        with _ -> location

    /// Atomically compares and exchanges two 64-bit integers
    let compareExchangeInt64 (location: byref<int64>) (value: int64) (comparand: int64): int64 =
        try
            Interlocked.CompareExchange(&location, value, comparand)
        with _ -> location

    /// Atomically compares and exchanges two reference types
    let compareExchangeRef<'T> (location: byref<'T>) (value: 'T) (comparand: 'T): 'T when 'T: class =
        try
            Interlocked.CompareExchange(&location, value, comparand)
        with _ -> location

    /// Reads a 32-bit value atomically
    let read (location: byref<int>): int =
        try
            Interlocked.Read(&location)
        with _ -> location

    /// Reads a 64-bit value atomically
    let readInt64 (location: byref<int64>): int64 =
        try
            Interlocked.Read(&location)
        with _ -> location

    /// Writes a 32-bit value atomically (use CompareExchange for compatibility)
    let write (location: byref<int>) (value: int): unit =
        try
            Interlocked.Exchange(&location, value) |> ignore
        with _ -> ()

    /// Writes a 64-bit value atomically (use CompareExchange for compatibility)
    let writeInt64 (location: byref<int64>) (value: int64): unit =
        try
            Interlocked.Exchange(&location, value) |> ignore
        with _ -> ()

    /// Atomically adds to a float32
    let addFloat32 (location: byref<float32>) (value: float32): float32 =
        try
            let mutable temp = location
            let mutable res = Unchecked.defaultof<float32>
            let mutable original = Unchecked.defaultof<float32>
            let mutable done_ = false
            while not done_ do
                res <- temp + value
                original <- Interlocked.CompareExchange(&location, res, temp)
                if original = temp then done_ <- true
                else temp <- original
            res
        with _ -> location + value

    /// Atomically adds to a float (float64)
    let addFloat (location: byref<float>) (value: float): float =
        try
            let mutable temp = location
            let mutable res = Unchecked.defaultof<float>
            let mutable original = Unchecked.defaultof<float>
            let mutable done_ = false
            while not done_ do
                res <- temp + value
                original <- Interlocked.CompareExchange(&location, res, temp)
                if original = temp then done_ <- true
                else temp <- original
            res
        with _ -> location + value
