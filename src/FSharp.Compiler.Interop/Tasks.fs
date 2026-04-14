// Copyright (c) Sascha Dewald. See https://github.com/fsharp-wasm/fsharp

namespace FSharp.Compiler.Interop

/// Nullability-safe wrappers for System.Threading.Tasks operations
module Tasks =
    open System
    open System.Threading
    open System.Threading.Tasks

    /// Creates a completed task with a result
    let createCompletedTask<'T> (value: 'T): Task<'T> =
        Task.FromResult(value)

    /// Creates a faulted task from an exception
    let createFaultedTask<'T> (exn: Exception): Task<'T> =
        let tcs = TaskCompletionSource<'T>()
        tcs.SetException(exn)
        tcs.Task

    /// Creates a canceled task
    let createCanceledTask<'T> (): Task<'T> =
        let tcs = TaskCompletionSource<'T>()
        tcs.SetCanceled()
        tcs.Task

    /// Gets the result of a task with timeout
    // let getTaskResult<'T> (task: Task<'T>) (timeoutMs: int option): 'T option =
    //     if isNull task then
    //         None
    //     else
    //         try
    //             let completed =
    //                 match timeoutMs with
    //                 | Some ms when ms > 0 -> task.Wait(ms) |> ignore; task.IsCompleted
    //                 | Some _ -> task.Wait(); true
    //                 | None -> task.Wait(); true

    //             if completed && not task.IsFaulted && not task.IsCanceled then
    //                 Some(task.Result)
    //             else
    //                 None
    //         with _ -> None

    /// Checks if a task is completed
    let isTaskCompleted<'T> (task: Task<'T>): bool =
        if isNull task then false
        else task.IsCompleted

    /// Checks if a task is faulted
    let isTaskFaulted<'T> (task: Task<'T>): bool =
        if isNull task then false
        else task.IsFaulted

    /// Checks if a task is canceled
    let isTaskCanceled<'T> (task: Task<'T>): bool =
        if isNull task then false
        else task.IsCanceled

    /// Gets the exception from a faulted task
    let getTaskException<'T> (task: Task<'T>): Exception option =
        if isNull task || not task.IsFaulted then
            None
        else
            try
                Some(task.Exception)
            with _ -> None

    /// Waits for a task to complete
    // let waitForTask<'T> (task: Task<'T>) (timeoutMs: int option): bool =
    //     if isNull task then false
    //     else
    //         try
    //             match timeoutMs with
    //             | Some ms when ms > 0 -> task.Wait(ms); true
    //             | Some _ -> task.Wait(); true
    //             | None -> task.Wait(); true
    //         with _ -> false

    /// Continues a task with another function that returns a task
    let continueTask<'T, 'U> (task: Task<'T>) (continuation: 'T -> Task<'U>): Task<'U> option =
        if isNull task then
            None
        else
            try
                if isNull (box continuation) then
                    None
                else
                    let result = task.ContinueWith(fun (t: Task<'T>) ->
                        if t.IsCompletedSuccessfully then
                            continuation t.Result
                        else
                            createFaultedTask(Exception("Prior task failed"))
                    )
                    Some(result.Unwrap())
            with _ -> None

    /// Creates a delay task
    let delayTask (millisecondsDelay: int): Task =
        if millisecondsDelay > 0 then
            Task.Delay(millisecondsDelay)
        else
            Task.Delay(0)

    /// Creates a delay task with cancellation token
    let delayTaskWithToken (millisecondsDelay: int) (cancellationToken: CancellationToken): Task =
        if millisecondsDelay > 0 then
            Task.Delay(millisecondsDelay, cancellationToken)
        else
            Task.Delay(0, cancellationToken)

    /// Waits for any task in a collection to complete
    let waitAny (tasks: Task[]): int option =
        if isNull tasks || Array.isEmpty tasks then
            None
        else
            try
                let index = Task.WaitAny(tasks)
                if index >= 0 then Some(index) else None
            with _ -> None

    /// Waits for all tasks in a collection to complete
    let waitAll (tasks: Task[]): bool =
        if isNull tasks || Array.isEmpty tasks then
            true
        else
            try
                Task.WaitAll(tasks)
                true
            with _ -> false

    /// Runs an async function as a task
    let runAsTask<'T> (asyncFunc: Func<Task<'T>>): Task<'T> option =
        if isNull asyncFunc then
            None
        else
            try
                Some(asyncFunc.Invoke())
            with _ -> None

    /// Creates a TaskCompletionSource
    let createTaskCompletionSource<'T> (): TaskCompletionSource<'T> =
        new TaskCompletionSource<'T>()

    /// Sets task completion result
    let setTaskResult<'T> (tcs: TaskCompletionSource<'T>) (result: 'T): bool =
        if isNull tcs then false
        else
            try
                tcs.SetResult(result)
                true
            with _ -> false

    /// Sets task completion exception
    let setTaskException<'T> (tcs: TaskCompletionSource<'T>) (exn: Exception): bool =
        if isNull tcs || isNull exn then false
        else
            try
                tcs.SetException(exn)
                true
            with _ -> false

    /// Sets task as canceled
    let setTaskCanceled<'T> (tcs: TaskCompletionSource<'T>): bool =
        if isNull tcs then false
        else
            try
                tcs.SetCanceled()
                true
            with _ -> false

    /// Tries to set task completion result
    let trySetTaskResult<'T> (tcs: TaskCompletionSource<'T>) (result: 'T): bool =
        if isNull tcs then false
        else
            try
                tcs.TrySetResult(result)
            with _ -> false

    /// Tries to set task completion exception
    let trySetTaskException<'T> (tcs: TaskCompletionSource<'T>) (exn: Exception): bool =
        if isNull tcs || isNull exn then false
        else
            try
                tcs.TrySetException(exn)
            with _ -> false

    /// Tries to set task as canceled
    let trySetTaskCanceled<'T> (tcs: TaskCompletionSource<'T>): bool =
        if isNull tcs then false
        else
            try
                tcs.TrySetCanceled()
            with _ -> false

    /// Runs an action synchronously and returns its result as a task
    let fromAction (action: Action): Task =
        if isNull action then
            createCompletedTask(())
        else
            try
                action.Invoke()
                Task.CompletedTask
            with exn -> Task.FromException(exn)

    /// Runs a function synchronously and returns its result as a task
    let fromFunc<'T> (func: Func<'T>): Task<'T> =
        if isNull func then
            createCompletedTask(Unchecked.defaultof<'T>)
        else
            try
                let result = func.Invoke()
                Task.FromResult(result)
            with exn -> createFaultedTask(exn)

    /// Creates a task that completes when any of the supplied tasks complete
    let whenAny (tasks: Task[]): Task<Task> option =
        if isNull tasks || Array.isEmpty tasks then
            None
        else
            try
                Some(Task.WhenAny(tasks))
            with _ -> None

    /// Creates a task that completes when any of the supplied tasks complete
    let whenAnyGeneric<'T> (tasks: Task<'T>[]): Task<Task<'T>> option =
        if isNull tasks || Array.isEmpty tasks then
            None
        else
            try
                Some(Task.WhenAny(tasks))
            with _ -> None

    /// Creates a task that completes when all tasks complete
    let whenAll (tasks: Task[]): Task option =
        if isNull tasks || Array.isEmpty tasks then
            Some(Task.CompletedTask)
        else
            try
                Some(Task.WhenAll(tasks))
            with _ -> None

    /// Creates a task that completes when all tasks complete
    let whenAllGeneric<'T> (tasks: Task<'T>[]): Task<'T[]> option =
        if isNull tasks || Array.isEmpty tasks then
            Some(Task.FromResult([||]))
        else
            try
                Some(Task.WhenAll(tasks))
            with _ -> None

    /// Checks if a task supports cancellation
    let supportsCancellation (task: Task): bool =
        if isNull task then false
        else
            try
                task.IsCanceled || (not task.IsCompleted && not task.IsFaulted)
            with _ -> false

    /// Gets the task status
    let getTaskStatus (task: Task): TaskStatus option =
        if isNull task then None
        else
            try
                Some(task.Status)
            with _ -> None

    /// Registers a continuation that runs when task completes
    let onTaskCompleted<'T> (task: Task<'T>) (continuation: unit -> unit): IDisposable option =
        if isNull task then
            None
        else
            try
                if isNull (box continuation) then
                    None
                else
                    let cts = new CancellationTokenSource()
                    let action : Action<Task<'T>> = Action<Task<'T>>(fun _ -> continuation())
                    let _t = task.ContinueWith(action)
                    Some(cts)
            with _ -> None

    /// Gets a task's id
    let getTaskId (task: Task): int option =
        if isNull task then None
        else
            try
                Some(task.Id)
            with _ -> None

    /// Gets a task's factory
    let getTaskFactory (): TaskFactory =
        Task.Factory
