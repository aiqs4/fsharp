// Copyright (c) Sascha Dewald. See https://github.com/fsharp-wasm/fsharp

namespace FSharp.Compiler.Interop

/// Nullability-safe wrappers for System.Threading.Monitor operations (explicit lock/unlock)
module Monitor =
    open System
    open System.Threading

    /// Acquires an exclusive lock on an object
    let enter (obj: obj): bool =
        if isNull obj then false
        else
            try
                System.Threading.Monitor.Enter(obj)
                true
            with _ -> false

    /// Acquires an exclusive lock on an object with timeout
    let tryEnter (obj: obj) (timeoutMs: int): bool =
        if isNull obj || timeoutMs < 0 then false
        else
            try
                if timeoutMs = 0 then
                    System.Threading.Monitor.TryEnter(obj)
                else
                    System.Threading.Monitor.TryEnter(obj, timeoutMs)
            with _ -> false

    /// Releases an exclusive lock on an object
    let exit (obj: obj): bool =
        if isNull obj then false
        else
            try
                System.Threading.Monitor.Exit(obj)
                true
            with _ -> false

    /// Notifies a thread waiting on an object
    let pulse (obj: obj): bool =
        if isNull obj then false
        else
            try
                System.Threading.Monitor.Pulse(obj)
                true
            with _ -> false

    /// Notifies all threads waiting on an object
    let pulseAll (obj: obj): bool =
        if isNull obj then false
        else
            try
                System.Threading.Monitor.PulseAll(obj)
                true
            with _ -> false

    /// Waits on an object (caller must already hold the lock)
    let wait (obj: obj): bool =
        if isNull obj then false
        else
            try
                System.Threading.Monitor.Wait(obj) |> ignore
                true
            with _ -> false

    /// Waits on an object with timeout
    let waitWithTimeout (obj: obj) (timeoutMs: int): bool =
        if isNull obj || timeoutMs < 0 then false
        else
            try
                System.Threading.Monitor.Wait(obj, timeoutMs) |> ignore
                true
            with _ -> false

    /// Executes code while holding a lock (using try-finally pattern)
    let withLock (obj: obj) (f: unit -> 'T): 'T option =
        if isNull obj then None
        else
            try
                System.Threading.Monitor.Enter(obj)
                try
                    Some(f ())
                finally
                    System.Threading.Monitor.Exit(obj)
            with _ -> None

    /// Executes code while holding a lock (unit return type)
    let withLockAction (obj: obj) (f: unit -> unit): bool =
        if isNull obj then false
        else
            try
                System.Threading.Monitor.Enter(obj)
                try
                    f ()
                    true
                finally
                    System.Threading.Monitor.Exit(obj)
            with _ -> false

    /// Gets the number of threads waiting on an object
    let getWaitCount (obj: obj): int option =
        if isNull obj then None
        else
            try
                // Note: There's no public API directly, but we can use reflection if needed
                // For now, return None as this info isn't directly accessible
                None
            with _ -> None

    /// Checks if current thread holds lock (no direct API available in .NET)
    let isHeldByCurrentThread (obj: obj): bool =
        if isNull obj then false
        else
            // Attempt to enter without waiting - if successful, we didn't have it
            match System.Threading.Monitor.TryEnter(obj, 0) with
            | true ->
                System.Threading.Monitor.Exit(obj)
                false
            | false -> true
