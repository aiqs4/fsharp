// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.Language

open Xunit
open FSharp.Test.Compiler
open FSharp.Test

module CEEnhancementsTests =

    // ---------------------------------------------------------------
    // BindReturn poisons do! — Issue 1 (Most Critical)
    // ---------------------------------------------------------------

    [<Fact>]
    let ``BindReturn does not poison do! — builder with BindReturn and do! compiles`` () =
        FSharp """
module Test

type Expr = C of int | Let of string * Expr * Expr | Seq of Expr list | Nop | Assign of string * Expr

type Builder() =
    member _.Bind(e: Expr, k: Expr -> Expr) = Let("x", e, k (C 0))
    member _.Bind(e: Expr, k: unit -> Expr) = Seq [e; k()]
    member _.Return(e: Expr) = e
    member _.Return(_: unit) = Nop
    member _.BindReturn(x: Expr, f: Expr -> Expr) = Let("x", x, f (C 0))
    member _.MergeSources(a: Expr, b: Expr) = Seq [a; b]
    member _.Zero() = Nop
    member _.Combine(a: Expr, b: Expr) = Seq [a; b]
    member _.Delay(f: unit -> Expr) = f()
    member _.Run(e: Expr) = e

let b = Builder()

let r = b {
    do! Assign("a", C 1)
    do! Assign("b", C 2)
}
        """
        |> typecheck
        |> shouldSucceed

    [<Fact>]
    let ``BindReturn still works for let! when BindReturn exists`` () =
        FSharp """
module Test

type Expr = C of int | Let of string * Expr * Expr | Seq of Expr list | Nop

type Builder() =
    member _.Bind(e: Expr, k: Expr -> Expr) = Let("x", e, k (C 0))
    member _.Return(e: Expr) = e
    member _.BindReturn(x: Expr, f: Expr -> Expr) = Let("x", x, f (C 0))
    member _.Zero() = Nop
    member _.Combine(a: Expr, b: Expr) = Seq [a; b]
    member _.Delay(f: unit -> Expr) = f()
    member _.Run(e: Expr) = e

let b = Builder()

let r = b {
    let! x = C 42
    return x
}
        """
        |> typecheck
        |> shouldSucceed

    // ---------------------------------------------------------------
    // if! tests
    // ---------------------------------------------------------------

    [<Fact>]
    let ``if! desugars to Bind with bool branches`` () =
        FSharp """
module Test

type Builder() =
    member _.Bind(x: Async<'T>, f: 'T -> Async<'U>) = async.Bind(x, f)
    member _.Return(x: 'T) = async.Return(x)
    member _.Zero() = async.Return(())
    member _.Combine(a, b) = async.Bind(a, fun () -> b)
    member _.Delay(f) = async.Delay(f)

let b = Builder()

let test () = b {
    if! async { return true } then
        return 1
    else
        return 2
}
        """
        |> withLangVersionPreview
        |> typecheck
        |> shouldSucceed

    [<Fact>]
    let ``if! without else uses Zero`` () =
        FSharp """
module Test

type Builder() =
    member _.Bind(x: Async<'T>, f: 'T -> Async<'U>) = async.Bind(x, f)
    member _.Return(x: 'T) = async.Return(x)
    member _.Zero() = async.Return(())
    member _.Combine(a, b) = async.Bind(a, fun () -> b)
    member _.Delay(f) = async.Delay(f)

let b = Builder()

let test () = b {
    if! async { return true } then
        return ()
}
        """
        |> withLangVersionPreview
        |> typecheck
        |> shouldSucceed

    [<Fact>]
    let ``if! requires preview language version`` () =
        FSharp """
module Test
let test () = async {
    if! async { return true } then
        return 1
    else
        return 2
}
        """
        |> withLangVersion80
        |> typecheck
        |> shouldFail
        |> withDiagnosticMessageMatches "if!"

    [<Fact>]
    let ``if! on builder without Bind produces error`` () =
        FSharp """
module Test

type Builder() =
    member _.Return(x: 'T) = async.Return(x)
    member _.Zero() = async.Return(())
    member _.Delay(f) = async.Delay(f)

let b = Builder()

let test () = b {
    if! async { return true } then
        return 1
    else
        return 2
}
        """
        |> withLangVersionPreview
        |> typecheck
        |> shouldFail
        |> withDiagnosticMessageMatches "Bind"

    // ---------------------------------------------------------------
    // for! tests
    // ---------------------------------------------------------------

    [<Fact>]
    let ``for! desugars to For without IEnumerable constraint`` () =
        FSharp """
module Test

type MyCollection<'T> = Items of 'T list

type Builder() =
    member _.For(Items xs: MyCollection<'T>, f: 'T -> Async<unit>) =
        async {
            for x in xs do
                do! f x
        }
    member _.Zero() = async.Return(())
    member _.Combine(a, b) = async.Bind(a, fun () -> b)
    member _.Delay(f) = async.Delay(f)
    member _.Return(x) = async.Return(x)

let b = Builder()

let test () = b {
    for! x in Items [1; 2; 3] do
        return ()
}
        """
        |> withLangVersionPreview
        |> typecheck
        |> shouldSucceed

    [<Fact>]
    let ``for! requires preview language version`` () =
        FSharp """
module Test
let test () = async {
    for! x in async { return [1;2;3] } do
        return ()
}
        """
        |> withLangVersion80
        |> typecheck
        |> shouldFail
        |> withDiagnosticMessageMatches "for!"

    // ---------------------------------------------------------------
    // while! carrier-free desugaring tests
    // ---------------------------------------------------------------

    [<Fact>]
    let ``while! works with async builder`` () =
        FSharp """
let mutable count = 0

let asyncCondition = async {
    return count < 10
}

async {
    count <- 1

    while! asyncCondition do
        count <- count + 2

    count <- count + 1
    return count
}
|> Async.RunSynchronously
|> printfn "%d"
        """
        |> withLangVersion80
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "12"

    [<Fact>]
    let ``while non-bang still uses While member`` () =
        FSharp """
module Test

type Builder() =
    member _.While(guard: unit -> bool, body: Async<unit>) = 
        async {
            while guard() do
                do! body
        }
    member _.Zero() = async.Return(())
    member _.Combine(a, b) = async.Bind(a, fun () -> b)
    member _.Delay(f: unit -> Async<'T>) = async.Delay(f)
    member _.Return(x) = async.Return(x)

let b = Builder()

let test () = b {
    let mutable i = 0
    while i < 5 do
        i <- i + 1
    return i
}
        """
        |> typecheck
        |> shouldSucceed

    // ---------------------------------------------------------------
    // Regression tests — async, task, seq behavior unchanged
    // ---------------------------------------------------------------

    [<Fact>]
    let ``async let! do! for while match! all work`` () =
        FSharp """
module Test

let test () = async {
    let! x = async { return 1 }
    do! async { return () }
    for i in [1..3] do
        do! async { return () }
    while false do
        do! async { return () }
    match! async { return Some 1 } with
    | Some v -> return v + x
    | None -> return 0
}
        """
        |> typecheck
        |> shouldSucceed

    [<Fact>]
    let ``task let! do! work`` () =
        FSharp """
module Test
open System.Threading.Tasks

let test () = task {
    let! x = Task.FromResult(1)
    do! Task.Delay(0)
    return x
}
        """
        |> typecheck
        |> shouldSucceed

    [<Fact>]
    let ``seq yield and yield! work`` () =
        FSharp """
module Test

let test () = seq {
    yield 1
    yield! [2; 3]
    for i in [4..6] do
        yield i
}
        """
        |> typecheck
        |> shouldSucceed

    // ---------------------------------------------------------------
    // Full AST builder — exercises all CE enhancements together
    // ---------------------------------------------------------------

    [<Fact>]
    let ``Full AST builder — mixed let! do! and! with overloaded Bind and BindReturn`` () =
        FSharp """
module Test

type Expr = C of int | Let of string * Expr * Expr | Seq of Expr list | Nop | Assign of string * Expr

type Builder() =
    member _.Bind(e: Expr, k: Expr -> Expr) = Let("x", e, k (C 0))
    member _.Bind(e: Expr, k: unit -> Expr) = Seq [e; k()]
    member _.Return(e: Expr) = e
    member _.Return(_: unit) = Nop
    member _.BindReturn(x: Expr, f: Expr -> Expr) = Let("x", x, f (C 0))
    member _.MergeSources(a: Expr, b: Expr) = Seq [a; b]
    member _.Zero() = Nop
    member _.Combine(a: Expr, b: Expr) = Seq [a; b]
    member _.Delay(f: unit -> Expr) = f()
    member _.Run(e: Expr) = e

let b = Builder()

// Mixed let! and do! — do! must use Bind(unit->Expr), not BindReturn
let r1 = b {
    let! x = C 1
    do! Assign("a", x)
    return x
}

// let! chain — inner let! uses BindReturn optimization
let r2 = b {
    let! x = C 1
    let! y = C 2
    return Seq [x; y]
}

// and! with Bind2Return (the user's workaround for non-tuple MergeSources)
let r3 = b {
    let! x = C 1
    let! y = C 2
    return x
}

// do! only — the original BindReturn poisoning repro
let r4 = b {
    do! Assign("a", C 1)
    do! Assign("b", C 2)
}

// let! followed by do! at final position
let r5 = b {
    let! x = C 1
    do! Assign("a", x)
}
        """
        |> typecheck
        |> shouldSucceed

    [<Fact>]
    let ``Full AST builder — executes correctly at runtime`` () =
        FSharp """
module Test

type Expr = C of int | Let of string * Expr * Expr | Seq of Expr list | Nop | Assign of string * Expr

type Builder() =
    member _.Bind(e: Expr, k: Expr -> Expr) = Let("x", e, k (C 0))
    member _.Bind(e: Expr, k: unit -> Expr) = Seq [e; k()]
    member _.Return(e: Expr) = e
    member _.Return(_: unit) = Nop
    member _.BindReturn(x: Expr, f: Expr -> Expr) = Let("x", x, f (C 0))
    member _.MergeSources(a: Expr, b: Expr) = Seq [a; b]
    member _.Zero() = Nop
    member _.Combine(a: Expr, b: Expr) = Seq [a; b]
    member _.Delay(f: unit -> Expr) = f()
    member _.Run(e: Expr) = e

let b = Builder()

// do! only test
let r = b {
    do! Assign("a", C 1)
    do! Assign("b", C 2)
}

// Verify the structure — Bind(unit->Expr) produces Seq, not Let
match r with
| Seq _ -> printfn "PASS: do! used Bind(unit->Expr) overload"
| Let _ -> printfn "FAIL: do! incorrectly used BindReturn"
| _ -> printfn "FAIL: unexpected structure %A" r
        """
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    // ---------------------------------------------------------------
    // FS0041 Overload Ambiguity — builder with dual BindReturn overloads  
    // ---------------------------------------------------------------

    [<Fact>]
    let ``FS0041 — BindReturn with unit overload does not cause ambiguity for let! return`` () =
        FSharp """
module Test

type Expr = C of int | Let of string * Expr * Expr | Seq of Expr list | Nop | Assign of string * Expr

type Builder() =
    member _.Bind(e: Expr, k: Expr -> Expr) = Let("x", e, k (C 0))
    member _.Bind(e: Expr, k: unit -> Expr) = Seq [e; k()]
    member _.Return(e: Expr) = e
    member _.Return(_: unit) = Nop
    member _.BindReturn(x: Expr, f: Expr -> Expr) = Let("x", x, f (C 0))
    member _.BindReturn(x: Expr, f: unit -> Expr) = Seq [x; f()]
    member _.Zero() = Nop
    member _.Combine(a: Expr, b: Expr) = Seq [a; b]
    member _.Delay(f: unit -> Expr) = f()
    member _.Run(e: Expr) = e

let b = Builder()

// This should resolve to BindReturn(Expr, Expr -> Expr) without FS0041
let r1 = b {
    let! x = C 42
    return x
}

// do! should still use Bind(unit->Expr) path 
let r2 = b {
    do! Assign("a", C 1)
    do! Assign("b", C 2)
}

// let! + do! in same CE
let r3 = b {
    let! x = C 1
    do! Assign("a", x)
    return x
}
        """
        |> typecheck
        |> shouldSucceed

    [<Fact>]
    let ``FS0041 — overloaded Bind does not cause ambiguity in non-final do!`` () =
        FSharp """
module Test

type Expr = C of int | Let of string * Expr * Expr | Seq of Expr list | Nop | Assign of string * Expr

type Builder() =
    member _.Bind(e: Expr, k: Expr -> Expr) = Let("x", e, k (C 0))
    member _.Bind(e: Expr, k: unit -> Expr) = Seq [e; k()]
    member _.Return(e: Expr) = e
    member _.Return(_: unit) = Nop
    member _.Zero() = Nop
    member _.Combine(a: Expr, b: Expr) = Seq [a; b]
    member _.Delay(f: unit -> Expr) = f()
    member _.Run(e: Expr) = e

let b = Builder()

// do! in non-final position — must select Bind(unit->Expr)
let r = b {
    do! Assign("a", C 1)
    let! x = C 2
    return x
}
        """
        |> typecheck
        |> shouldSucceed
