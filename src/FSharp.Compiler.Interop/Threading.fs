// Copyright (c) Sascha Dewald. See https://github.com/fsharp-wasm/fsharp

namespace FSharp.Compiler.Interop

/// Nullability-safe wrappers for System.Threading operations
module Threading =
    open System
    open System.Threading

    /// Gets the current thread
    let getCurrentThread (): Thread option =
        try
            Some(Thread.CurrentThread)
        with _ -> None

    /// Gets the thread ID
    let getThreadId (thread: Thread): int =
        if isNull thread then -1
        else
            try
                thread.ManagedThreadId
            with _ -> -1

    /// Gets the thread name
    let getThreadName (thread: Thread): string option =
        if isNull thread then None
        else
            try
                let name = thread.Name
                if String.isNullOrEmpty name then None else Some name
            with _ -> None

    /// Sets the thread name
    let setThreadName (thread: Thread) (name: string): bool =
        if isNull thread || String.isNullOrEmpty name then
            false
        else
            try
                thread.Name <- name
                true
            with _ -> false

    /// Checks if a thread is alive
    let isThreadAlive (thread: Thread): bool =
        if isNull thread then false
        else
            try
                thread.IsAlive
            with _ -> false

    /// Gets the thread state
    let getThreadState (thread: Thread): ThreadState option =
        if isNull thread then None
        else
            try
                Some(thread.ThreadState)
            with _ -> None

    /// Joins a thread with optional timeout
    let joinThread (thread: Thread) (timeoutMs: int option): bool =
        if isNull thread then false
        else
            try
                match timeoutMs with
                | Some ms when ms > 0 -> thread.Join(ms) |> ignore; true
                | Some _ -> thread.Join(); true
                | None -> thread.Join(); true
            with _ -> false

    /// Aborts a thread (deprecated but available)
    // let abortThread (thread: Thread): bool =
    //     if isNull thread then false
    //     else
    //         try
    //             thread.Abort()
    //             true
    //         with _ -> false

    /// Creates a CancellationTokenSource
    let createCancellationTokenSource (): CancellationTokenSource =
        new CancellationTokenSource()

    /// Creates a CancellationTokenSource with timeout
    let createCancellationTokenSourceWithTimeout (timeoutMs: int): CancellationTokenSource =
        if timeoutMs > 0 then
            new CancellationTokenSource(timeoutMs)
        else
            new CancellationTokenSource()

    /// Gets the cancellation token from a source
    let getCancellationToken (source: CancellationTokenSource): CancellationToken option =
        if isNull source then None
        else
            try
                Some(source.Token)
            with _ -> None

    /// Checks if cancellation has been requested
    let isCancellationRequested (token: CancellationToken): bool =
        token.IsCancellationRequested

    /// Registers a callback for when cancellation is requested
    let registerCancellationCallback (token: CancellationToken) (callback: unit -> unit): IDisposable option =
        try
            if isNull (box callback) then None
            else
                Some(token.Register(callback))
        with _ -> None

    /// Throws if cancellation has been requested
    let throwIfCancellationRequested (token: CancellationToken): unit =
        try
            token.ThrowIfCancellationRequested()
        with _ -> ()

    /// Cancels a CancellationTokenSource
    let cancelTokenSource (source: CancellationTokenSource): bool =
        if isNull source then false
        else
            try
                source.Cancel()
                true
            with _ -> false

    /// Disposes a CancellationTokenSource
    let disposeTokenSource (source: CancellationTokenSource): bool =
        if isNull source then false
        else
            try
                source.Dispose()
                true
            with _ -> false

    /// Creates a ManualResetEvent
    let createManualResetEvent (initialState: bool): ManualResetEvent =
        new ManualResetEvent(initialState)

    /// Sets a ManualResetEvent
    let setEvent (evt: ManualResetEvent): bool =
        if isNull evt then false
        else
            try
                evt.Set() |> ignore
                true
            with _ -> false

    /// Resets a ManualResetEvent
    let resetEvent (evt: ManualResetEvent): bool =
        if isNull evt then false
        else
            try
                evt.Reset() |> ignore
                true
            with _ -> false

    /// Waits on a ManualResetEvent
    let waitEvent (evt: ManualResetEvent) (timeoutMs: int option): bool =
        if isNull evt then false
        else
            try
                match timeoutMs with
                | Some ms when ms > 0 -> evt.WaitOne(ms)
                | Some _ -> evt.WaitOne()
                | None -> evt.WaitOne()
            with _ -> false

    /// Creates an AutoResetEvent
    let createAutoResetEvent (initialState: bool): AutoResetEvent =
        new AutoResetEvent(initialState)

    /// Creates a Semaphore
    let createSemaphore (initialCount: int) (maxCount: int): Semaphore option =
        if initialCount < 0 || maxCount <= 0 || initialCount > maxCount then
            None
        else
            try
                Some(new Semaphore(initialCount, maxCount))
            with _ -> None

    /// Waits on a Semaphore
    let waitSemaphore (sem: Semaphore) (timeoutMs: int option): bool =
        if isNull sem then false
        else
            try
                match timeoutMs with
                | Some ms when ms > 0 -> sem.WaitOne(ms)
                | Some _ -> sem.WaitOne()
                | None -> sem.WaitOne()
            with _ -> false

    /// Releases a Semaphore
    let releaseSemaphore (sem: Semaphore): int option =
        if isNull sem then None
        else
            try
                Some(sem.Release())
            with _ -> None

    /// Creates a Mutex
    let createMutex (initiallyOwned: bool option): Mutex option =
        try
            Some(new Mutex(defaultArg initiallyOwned false))
        with _ -> None

    /// Waits on a Mutex
    let waitMutex (mutex: Mutex) (timeoutMs: int option): bool =
        if isNull mutex then false
        else
            try
                match timeoutMs with
                | Some ms when ms > 0 -> mutex.WaitOne(ms)
                | Some _ -> mutex.WaitOne()
                | None -> mutex.WaitOne()
            with _ -> false

    /// Releases a Mutex
    let releaseMutex (mutex: Mutex): bool =
        if isNull mutex then false
        else
            try
                mutex.ReleaseMutex()
                true
            with _ -> false

    /// Creates a ReaderWriterLockSlim
    let createReaderWriterLock (): ReaderWriterLockSlim =
        new ReaderWriterLockSlim()

    /// Acquires a read lock
    let acquireReadLock (lock: ReaderWriterLockSlim) (timeoutMs: int option): bool =
        if isNull lock then false
        else
            try
                match timeoutMs with
                | Some ms when ms > 0 -> lock.TryEnterReadLock(ms)
                | Some _ -> lock.EnterReadLock(); true
                | None -> lock.EnterReadLock(); true
            with _ -> false

    /// Releases a read lock
    let releaseReadLock (lock: ReaderWriterLockSlim): bool =
        if isNull lock then false
        else
            try
                lock.ExitReadLock()
                true
            with _ -> false

    /// Acquires a write lock
    let acquireWriteLock (lock: ReaderWriterLockSlim) (timeoutMs: int option): bool =
        if isNull lock then false
        else
            try
                match timeoutMs with
                | Some ms when ms > 0 -> lock.TryEnterWriteLock(ms)
                | Some _ -> lock.EnterWriteLock(); true
                | None -> lock.EnterWriteLock(); true
            with _ -> false

    /// Releases a write lock
    let releaseWriteLock (lock: ReaderWriterLockSlim): bool =
        if isNull lock then false
        else
            try
                lock.ExitWriteLock()
                true
            with _ -> false

    /// Sleeps the current thread
    let sleep (millisecondsTimeout: int): unit =
        try
            if millisecondsTimeout > 0 then
                Thread.Sleep(millisecondsTimeout)
            else
                Thread.Sleep(0)
        with _ -> ()

    /// Yields execution to other threads
    let yield_ (): unit =
        Thread.Yield() |> ignore

    /// Gets the processor count
    let getProcessorCount (): int =
        try
            Environment.ProcessorCount
        with _ -> 1

    /// Creates a ThreadPool
    let getThreadPoolInfo (): (int * int) option =
        try
            let mutable workerThreads = 0
            let mutable completionPortThreads = 0
            ThreadPool.GetAvailableThreads(&workerThreads, &completionPortThreads)
            Some(workerThreads, completionPortThreads)
        with _ -> None

    /// Queues work to thread pool
    let queueUserWorkItem (workItem: WaitCallback) (state: obj): bool =
        if isNull workItem then false
        else
            try
                ThreadPool.QueueUserWorkItem(workItem, state) |> ignore
                true
            with _ -> false
