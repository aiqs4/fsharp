// Copyright (c) Sascha Dewald. See https://github.com/fsharp-wasm/fsharp

namespace FSharp.Compiler.Interop

/// Nullability-safe wrappers for System.Diagnostics.Stopwatch operations
module Stopwatch =
    open System
    open System.Diagnostics

    /// Creates a new Stopwatch instance
    let create (): Stopwatch option =
        try
            Some(new Stopwatch())
        with _ -> None

    /// Creates and starts a new Stopwatch
    let startNew (): Stopwatch option =
        try
            Some(Stopwatch.StartNew())
        with _ -> None

    /// Starts a Stopwatch
    let start (sw: Stopwatch): bool =
        if isNull sw then false
        else
            try
                sw.Start()
                true
            with _ -> false

    /// Stops a Stopwatch
    let stop (sw: Stopwatch): bool =
        if isNull sw then false
        else
            try
                sw.Stop()
                true
            with _ -> false

    /// Restarts a Stopwatch
    let restart (sw: Stopwatch): bool =
        if isNull sw then false
        else
            try
                sw.Restart()
                true
            with _ -> false

    /// Resets a Stopwatch
    let reset (sw: Stopwatch): bool =
        if isNull sw then false
        else
            try
                sw.Reset()
                true
            with _ -> false

    /// Gets elapsed time as TimeSpan
    let getElapsed (sw: Stopwatch): TimeSpan option =
        if isNull sw then None
        else
            try
                Some(sw.Elapsed)
            with _ -> None

    /// Gets elapsed milliseconds
    let getElapsedMilliseconds (sw: Stopwatch): int64 option =
        if isNull sw then None
        else
            try
                Some(sw.ElapsedMilliseconds)
            with _ -> None

    /// Gets elapsed ticks
    let getElapsedTicks (sw: Stopwatch): int64 option =
        if isNull sw then None
        else
            try
                Some(sw.ElapsedTicks)
            with _ -> None

    /// Checks if Stopwatch is running
    let isRunning (sw: Stopwatch): bool =
        if isNull sw then false
        else
            try
                sw.IsRunning
            with _ -> false

    /// Gets frequency (ticks per second)
    let getFrequency (): int64 =
        try
            Stopwatch.Frequency
        with _ -> 0L

    /// Timing a function (returns elapsed milliseconds)
    let timeFunction<'T> (f: unit -> 'T): ('T * int64) option =
        try
            let sw = Stopwatch.StartNew()
            let result = f ()
            sw.Stop()
            Some(result, sw.ElapsedMilliseconds)
        with _ -> None

    /// Timing an action (returns elapsed milliseconds)
    let timeAction (f: unit -> unit): int64 option =
        try
            let sw = Stopwatch.StartNew()
            f ()
            sw.Stop()
            Some(sw.ElapsedMilliseconds)
        with _ -> None
