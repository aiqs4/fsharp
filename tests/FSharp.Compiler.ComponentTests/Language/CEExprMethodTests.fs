// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.Language

open Xunit
open FSharp.Test.Compiler
open FSharp.Test

/// Tests for WhileExpr, ForExpr, and IfExpr builder methods in computation expressions.
/// These methods allow CE builders to handle non-bang while/for/if with unconstrained types.
module CEExprMethodTests =

    // ---------------------------------------------------------------
    // WhileExpr tests
    // ---------------------------------------------------------------

    [<Fact>]
    let ``WhileExpr with custom non-bool guard type`` () =
        FSharp
            """
module Test

type Expr = Guard of string | Body of string | WhileLoop of Expr * Expr | Nop

type Builder() =
    member _.WhileExpr(guard: Expr, body: Expr) = WhileLoop(guard, body)
    member _.Delay(f: unit -> Expr) = f()
    member _.Zero() = Nop
    member _.Combine(a: Expr, b: Expr) = Body(sprintf "%A; %A" a b)
    member _.Run(e: Expr) = e

let b = Builder()

let result = b {
    while (Guard "cond") do
        ()
}

match result with
| WhileLoop(Guard "cond", Nop) -> printfn "PASS"
| x -> printfn "FAIL: %A" x
        """
        |> withLangVersionPreview
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    [<Fact>]
    let ``WhileExpr takes priority over While when both are defined`` () =
        FSharp
            """
module Test

type Expr = Guard of string | Body of string | WhileLoop of Expr * Expr | OldWhile of Expr | Nop

type Builder() =
    member _.WhileExpr(guard: Expr, body: Expr) = WhileLoop(guard, body)
    member _.While(guard: unit -> bool, body: Expr) = OldWhile(body)
    member _.Delay(f: unit -> Expr) = f()
    member _.Zero() = Nop
    member _.Combine(a: Expr, b: Expr) = Body(sprintf "%A; %A" a b)
    member _.Run(e: Expr) = e

let b = Builder()

// When WhileExpr is present, non-bang while uses WhileExpr, not While
let result = b {
    while (Guard "test") do
        ()
}

match result with
| WhileLoop(Guard "test", Nop) -> printfn "PASS"
| x -> printfn "FAIL: %A" x
        """
        |> withLangVersionPreview
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    [<Fact>]
    let ``WhileExpr with body containing do!`` () =
        FSharp
            """
module Test

type Expr =
    | C of int | Assign of string * Expr | WhileLoop of Expr * Expr
    | LG of string | Seq of Expr list | Nop

type Builder() =
    member _.WhileExpr(guard: Expr, body: Expr) = WhileLoop(guard, body)
    member _.Bind(e: Expr, k: unit -> Expr) = Seq [e; k()]
    member _.Delay(f: unit -> Expr) = f()
    member _.Zero() = Nop
    member _.Combine(a: Expr, b: Expr) = Seq [a; b]
    member _.Run(e: Expr) = e

let b = Builder()

let result = b {
    while (LG "cond") do
        do! Assign("x", C 1)
}

match result with
| WhileLoop(LG "cond", Seq [Assign("x", C 1); Nop]) -> printfn "PASS"
| x -> printfn "FAIL: %A" x
        """
        |> withLangVersionPreview
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    [<Fact>]
    let ``while still uses While method when WhileExpr is not defined`` () =
        FSharp
            """
module Test

type Builder() =
    member _.While(guard: unit -> bool, body: int) = if guard() then body else 0
    member _.Delay(f: unit -> int) = f()
    member _.Zero() = 0
    member _.Run(e: int) = e

let b = Builder()

let result = b {
    while false do
        ()
}

printfn "%d" result
        """
        |> withLangVersionPreview
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "0"

    [<Fact>]
    let ``WhileExpr requires preview language version`` () =
        FSharp
            """
module Test

type Expr = Guard of string | Nop | WhileLoop of Expr * Expr

type Builder() =
    member _.WhileExpr(guard: Expr, body: Expr) = WhileLoop(guard, body)
    member _.Delay(f: unit -> Expr) = f()
    member _.Zero() = Nop
    member _.Run(e: Expr) = e

let b = Builder()

let result = b {
    while (Guard "cond") do
        ()
}
        """
        |> withLangVersion80
        |> typecheck
        |> shouldFail

    // ---------------------------------------------------------------
    // ForExpr tests
    // ---------------------------------------------------------------

    [<Fact>]
    let ``ForExpr with custom non-IEnumerable collection type`` () =
        FSharp
            """
module Test

type Expr = Items of string | Body of string | ForLoop of Expr * (Expr -> Expr) | Nop | Seq of Expr list

type Builder() =
    member _.ForExpr(collection: Expr, body: Expr -> Expr) = ForLoop(collection, body)
    member _.Delay(f: unit -> Expr) = f()
    member _.Zero() = Nop
    member _.Combine(a: Expr, b: Expr) = Seq [a; b]
    member _.Run(e: Expr) = e

let b = Builder()

let result = b {
    for item in Items "arr" do
        ()
}

match result with
| ForLoop(Items "arr", f) ->
    match f (Body "test") with
    | Nop -> printfn "PASS"
    | x -> printfn "FAIL body: %A" x
| x -> printfn "FAIL: %A" x
        """
        |> withLangVersionPreview
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    [<Fact>]
    let ``ForExpr skips Source wrapping`` () =
        FSharp
            """
module Test

type Expr = Items of string | Body of string | ForLoop of Expr * (Expr -> Expr) | Nop

type Builder() =
    member _.ForExpr(collection: Expr, body: Expr -> Expr) = ForLoop(collection, body)
    member _.Source(x: seq<'T>) : seq<'T> = x  // Should NOT be called when ForExpr is used
    member _.For(items: seq<'T>, body: 'T -> Expr) = Nop  // Should NOT be called when ForExpr is used
    member _.Delay(f: unit -> Expr) = f()
    member _.Zero() = Nop
    member _.Run(e: Expr) = e

let b = Builder()

// ForExpr should be used, not For+Source
let result = b {
    for item in Items "myArray" do
        ()
}

match result with
| ForLoop(Items "myArray", _) -> printfn "PASS"
| x -> printfn "FAIL: %A" x
        """
        |> withLangVersionPreview
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    [<Fact>]
    let ``for still uses For method when ForExpr is not defined`` () =
        FSharp
            """
module Test

type Builder() =
    member _.For(items: seq<'T>, body: 'T -> int) = Seq.sumBy body items
    member _.Delay(f: unit -> int) = f()
    member _.Zero() = 0
    member _.Combine(a: int, b: int) = a + b
    member _.Run(e: int) = e

let b = Builder()

let result = b {
    for x in [1; 2; 3] do
        ()
}

printfn "%d" result
        """
        |> withLangVersionPreview
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "0"

    [<Fact>]
    let ``ForExpr requires preview language version`` () =
        FSharp
            """
module Test

type Expr = Items of string | ForLoop of Expr * (Expr -> Expr) | Nop

type Builder() =
    member _.ForExpr(collection: Expr, body: Expr -> Expr) = ForLoop(collection, body)
    member _.Delay(f: unit -> Expr) = f()
    member _.Zero() = Nop
    member _.Run(e: Expr) = e

let b = Builder()

let result = b {
    for item in Items "arr" do
        ()
}
        """
        |> withLangVersion80
        |> typecheck
        |> shouldFail

    // ---------------------------------------------------------------
    // IfExpr tests
    // ---------------------------------------------------------------

    [<Fact>]
    let ``IfExpr with both branches`` () =
        FSharp
            """
module Test

type Expr = Guard of string | Body of string | IfNode of Expr * Expr * Expr | Nop

type Builder() =
    member _.IfExpr(guard: Expr, thenBranch: Expr, elseBranch: Expr) = IfNode(guard, thenBranch, elseBranch)
    member _.Delay(f: unit -> Expr) = f()
    member _.Zero() = Nop
    member _.Combine(a: Expr, b: Expr) = Body(sprintf "%A; %A" a b)
    member _.Run(e: Expr) = e

let b = Builder()

let result = b {
    if (Guard "cond") then
        ()
    else
        ()
}

match result with
| IfNode(Guard "cond", Nop, Nop) -> printfn "PASS"
| x -> printfn "FAIL: %A" x
        """
        |> withLangVersionPreview
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    [<Fact>]
    let ``IfExpr with missing else uses Zero`` () =
        FSharp
            """
module Test

type Expr = Guard of string | Body of string | IfNode of Expr * Expr * Expr | Nop

type Builder() =
    member _.IfExpr(guard: Expr, thenBranch: Expr, elseBranch: Expr) = IfNode(guard, thenBranch, elseBranch)
    member _.Delay(f: unit -> Expr) = f()
    member _.Zero() = Nop
    member _.Combine(a: Expr, b: Expr) = Body(sprintf "%A; %A" a b)
    member _.Run(e: Expr) = e

let b = Builder()

let result = b {
    if (Guard "cond") then
        ()
}

match result with
| IfNode(Guard "cond", Nop, Nop) -> printfn "PASS"
| x -> printfn "FAIL: %A" x
        """
        |> withLangVersionPreview
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    [<Fact>]
    let ``IfExpr with non-bool guard type`` () =
        FSharp
            """
module Test

type Expr =
    | C of int | Assign of string * Expr | IfNode of Expr * Expr * Expr
    | LG of string | Seq of Expr list | Nop

type Builder() =
    member _.IfExpr(guard: Expr, thenBranch: Expr, elseBranch: Expr) = IfNode(guard, thenBranch, elseBranch)
    member _.Bind(e: Expr, k: unit -> Expr) = Seq [e; k()]
    member _.Delay(f: unit -> Expr) = f()
    member _.Zero() = Nop
    member _.Combine(a: Expr, b: Expr) = Seq [a; b]
    member _.Run(e: Expr) = e

let b = Builder()

let result = b {
    if (LG "flag") then
        do! Assign("x", C 1)
    else
        do! Assign("x", C 2)
}

match result with
| IfNode(LG "flag", Seq [Assign("x", C 1); Nop], Seq [Assign("x", C 2); Nop]) -> printfn "PASS"
| x -> printfn "FAIL: %A" x
        """
        |> withLangVersionPreview
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    [<Fact>]
    let ``if still uses standard desugaring when IfExpr is not defined`` () =
        FSharp
            """
module Test

type Builder() =
    member _.Zero() = 0
    member _.Combine(a: int, b: int) = a + b
    member _.Delay(f: unit -> int) = f()
    member _.Run(e: int) = e

let b = Builder()

let result = b {
    if true then
        ()
}

printfn "%d" result
        """
        |> withLangVersionPreview
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "0"

    [<Fact>]
    let ``IfExpr requires preview language version`` () =
        FSharp
            """
module Test

type Expr = Guard of string | IfNode of Expr * Expr * Expr | Nop

type Builder() =
    member _.IfExpr(guard: Expr, thenBranch: Expr, elseBranch: Expr) = IfNode(guard, thenBranch, elseBranch)
    member _.Delay(f: unit -> Expr) = f()
    member _.Zero() = Nop
    member _.Run(e: Expr) = e

let b = Builder()

let result = b {
    if (Guard "cond") then
        ()
}
        """
        |> withLangVersion80
        |> typecheck
        |> shouldFail

    // ---------------------------------------------------------------
    // Backward compatibility tests
    // ---------------------------------------------------------------

    [<Fact>]
    let ``seq while/for/if behavior unchanged`` () =
        FSharp
            """
module Test

let xs = seq {
    let mutable i = 0
    while i < 3 do
        yield i
        i <- i + 1
}

let ys = seq {
    for x in [10; 20; 30] do
        yield x + 1
}

let zs = seq {
    if true then
        yield 100
    else
        yield 200
}

printfn "%A" (Seq.toList xs)
printfn "%A" (Seq.toList ys)
printfn "%A" (Seq.toList zs)
        """
        |> withLangVersionPreview
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "[0; 1; 2]"
        |> withStdOutContains "[11; 21; 31]"
        |> withStdOutContains "[100]"

    [<Fact>]
    let ``async while/for/if behavior unchanged`` () =
        FSharp
            """
module Test

let test () = async {
    let mutable i = 0
    while i < 3 do
        i <- i + 1

    for x in [1; 2; 3] do
        i <- i + x

    if i > 5 then
        return i
    else
        return 0
}

test () |> Async.RunSynchronously |> printfn "%d"
        """
        |> withLangVersionPreview
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "9"

    // ---------------------------------------------------------------
    // Combined / full integration test
    // ---------------------------------------------------------------

    [<Fact>]
    let ``Full builder with WhileExpr, ForExpr, and IfExpr`` () =
        FSharp
            """
module Test

type Expr =
    | C of int | LG of string | Assign of string * Expr
    | WhileLoop of Expr * Expr | ForLoop of Expr * (Expr -> Expr) | IfNode of Expr * Expr * Expr
    | Seq of Expr list | Nop

type Builder() =
    member _.WhileExpr(guard: Expr, body: Expr) = WhileLoop(guard, body)
    member _.ForExpr(collection: Expr, body: Expr -> Expr) = ForLoop(collection, body)
    member _.IfExpr(guard: Expr, thenBranch: Expr, elseBranch: Expr) = IfNode(guard, thenBranch, elseBranch)
    member _.Bind(e: Expr, k: unit -> Expr) = Seq [e; k()]
    member _.Delay(f: unit -> Expr) = f()
    member _.Zero() = Nop
    member _.Combine(a: Expr, b: Expr) = Seq [a; b]
    member _.Run(e: Expr) = e

let b = Builder()

let result = b {
    while (LG "cond") do
        do! Assign("i", C 1)

    for item in (LG "arr") do
        do! Assign("acc", item)

    if (LG "flag") then
        do! Assign("x", C 10)
    else
        do! Assign("x", C 20)
}

let mutable pass = true

match result with
| Seq parts when parts.Length >= 3 ->
    match parts.[0] with
    | WhileLoop(LG "cond", _) -> ()
    | x -> printfn "FAIL while: %A" x; pass <- false

    match parts.[1] with
    | ForLoop(LG "arr", _) -> ()
    | x -> printfn "FAIL for: %A" x; pass <- false

    match parts.[2] with
    | IfNode(LG "flag", _, _) -> ()
    | x -> printfn "FAIL if: %A" x; pass <- false
| x -> printfn "FAIL overall: %A" x; pass <- false

if pass then printfn "PASS"
        """
        |> withLangVersionPreview
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"
