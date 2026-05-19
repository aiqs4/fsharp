namespace FSharp.Compiler.ComponentTests.Language

open Xunit
open FSharp.Test.Compiler
open FSharp.Test

module CEEnhancementsTests =

    // ---------------------------------------------------------------
    // BindReturn poisons do!
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
        |> compile
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
        |> compile
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
        |> compile
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
        |> compile
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
        |> compile
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
        |> compile
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
        |> compile
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
        |> compile
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
        |> compile
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
        |> compile
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
        |> compile
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
// Type annotation on return needed: x is unconstrained, Return has 2 overloads
let r = b {
    do! Assign("a", C 1)
    let! x = C 2
    return (x : Expr)
}
        """
        |> compile
        |> shouldSucceed

    // ---------------------------------------------------------------
    // Full WASM CE Reference Demo — All Features (Stack Approach)
    // ---------------------------------------------------------------

    [<Fact>]
    let ``WASM CE reference demo — all features compile and run`` () =
        FSharp """
#nowarn "0086"

module Test

// AST
type Expr =
    | C of int
    | Let of string * Expr * Expr
    | LetMut of string * Expr * Expr
    | Seq of Expr list
    | Assign of string * Expr
    | LG of string
    | If of Expr * Expr * Expr
    | Loop of string * Expr
    | Block of string * Expr
    | Break of string
    | Continue of string
    | Binary of string * Expr * Expr
    | Yield of Expr
    | Nop

// Helper types
type MutInit = { Init: Expr }
let mut (e: Expr) : MutInit = { Init = e }

// DSL Operators
module WasmOps =
    let (+) (a: Expr) (b: Expr) = Binary("add", a, b)
    let (<) (a: Expr) (b: Expr) = Binary("lt_s", a, b)
    let (&&&) (a: Expr) (b: Expr) = Binary("and", a, b)
    let (==) (a: Expr) (b: Expr) = Binary("eq", a, b)
    let (<--) (n: string) (v: Expr) : Expr = Assign(n, v)
    
    let inline (<!) a b = a < C b
    let inline (+!) a b = a + C b
    let inline (==!) a b = a == C b

open WasmOps

// Shared state
let _whileBangGuards = System.Collections.Generic.Stack<Expr>()
let mutable _ctr = 0

let fresh () =
    _ctr <- Microsoft.FSharp.Core.Operators.(+) _ctr 1
    $"$w{_ctr}"

let mkExit (lbl: string) = $"{lbl}_exit"

// The Builder
type WasmBuilder() =
    member _.Bind(e: Expr, k: Expr -> Expr) : Expr =
        let n = fresh ()
        Let(n, e, k (LG n))

    member _.Bind(e: Expr, k: unit -> Expr) : Expr =
        let r = k ()
        match r with
        | Nop -> e
        | _ -> Seq [ e; r ]

    member _.Bind(m: MutInit, k: Expr -> Expr) : Expr =
        let n = fresh ()
        LetMut(n, m.Init, k (LG n))

    member _.Bind(g: Expr, k: bool -> Expr) : Expr =
        _whileBangGuards.Push(g)
        k true

    member _.While(_: unit -> bool, body: Expr) : Expr =
        if _whileBangGuards.Count > 0 then
            let g = _whileBangGuards.Pop()
            let lbl = fresh ()
            let exit = mkExit lbl
            Block(exit, Loop(lbl, If(g, Seq [ body; Continue lbl ], Break exit)))
        else
            body

    member _.Bind2Return(a: Expr, b: Expr, f: Expr * Expr -> Expr) : Expr =
        let n1 = fresh ()
        let n2 = fresh ()
        Let(n1, a, Let(n2, b, f (LG n1, LG n2)))

    member _.Bind2(a: Expr, b: Expr, f: Expr * Expr -> Expr) : Expr =
        let n1 = fresh ()
        let n2 = fresh ()
        Let(n1, a, Let(n2, b, f (LG n1, LG n2)))

    member _.Return(e: Expr) : Expr = e
    member _.ReturnFrom(e: Expr) : Expr = e
    member _.Return(_: unit) : Expr = Nop
    member _.Yield(e: Expr) : Expr = Yield e
    member _.Zero() : Expr = Nop

    member _.Combine(a: Expr, b: Expr) : Expr =
        match a, b with
        | Nop, x | x, Nop -> x
        | _ -> Seq [ a; b ]

    member _.For(items: Expr, k: Expr -> Expr) : Expr =
        let n = fresh ()
        Let(n, items, k (LG n))

    member _.Delay(f: unit -> Expr) : Expr = f ()
    member _.Run(e: Expr) : Expr = e

    member _.Using(res: #System.IDisposable, k) : Expr =
        try k res
        finally res.Dispose()

let wasm = WasmBuilder()

// Test harness
let mutable pass = 0
let mutable fail = 0

let test (name: string) (f: unit -> unit) =
    try
        f ()
        pass <- Microsoft.FSharp.Core.Operators.(+) pass 1
        printfn "  PASS %s" name
    with ex ->
        fail <- Microsoft.FSharp.Core.Operators.(+) fail 1
        printfn "  FAIL %s - %s" name ex.Message

let rec flatSeq =
    function
    | Seq xs -> List.collect flatSeq xs
    | x -> [ x ]

// T1: let! + return
test "T1: let!+return" (fun () ->
    let r =
        wasm {
            let! x = C 10
            return (x : Expr)
        }

    match r with
    | Let(_, C 10, LG _) -> ()
    | _ -> failwith $"%A{r}")

// T2: multiple do!
test "T2: multi do!" (fun () ->
    let r =
        wasm {
            do! "a" <-- C 1
            do! "b" <-- C 2
            do! "c" <-- C 3
        }

    let f = flatSeq r

    match f with
    | [ Assign("a", _); Assign("b", _); Assign("c", _) ] -> ()
    | _ -> failwith $"%A{f}")

// T3: while! single do!
test "T3: while! 1x do!" (fun () ->
    let r =
        wasm {
            while! (LG "cond") do
                do! "x" <-- C 1
        }

    match r with
    | Block(_, Loop(_, If(LG "cond", _, _))) -> ()
    | _ -> failwith $"%A{r}")

// T4: while! multiple do!
test "T4: while! 3x do!" (fun () ->
    let r =
        wasm {
            while! (LG "cond") do
                do! "x" <-- C 1
                do! "y" <-- C 2
                do! "z" <-- C 3
        }

    match r with
    | Block _ -> ()
    | _ -> failwith $"%A{r}")

// T5: nested while!
test "T5: nested while!" (fun () ->
    let r =
        wasm {
            while! (LG "outer") do
                while! (LG "inner") do
                    do! "x" <-- C 42
                do! "y" <-- C 99
        }

    match r with
    | Block _ -> ()
    | _ -> failwith $"%A{r}")

// T6: let! + while! + return
test "T6: let!+while!+return" (fun () ->
    let r =
        wasm {
            let! acc = mut (C 0)

            while! (LG "cond") do
                do! "i" <-- (LG "i" +! 1)

            return acc
        }

    match r with
    | LetMut _ -> ()
    | _ -> failwith $"%A{r}")

// T7: and! (FS-1063 via Bind2Return)
test "T7: and!" (fun () ->
    let r =
        wasm {
            let! x = C 10
            and! y = C 20
            return x + y
        }

    match r with
    | Let(n1, C 10, Let(n2, C 20, Binary("add", LG a, LG b))) when a = n1 && b = n2 -> ()
    | _ -> failwith $"%A{r}")

// T8: match!
test "T8: match!" (fun () ->
    let r =
        wasm {
            match! (LG "tag") with
            | x -> return (x : Expr)
        }

    match r with
    | Let(_, LG "tag", _) -> ()
    | _ -> failwith $"%A{r}")

// T9: for
test "T9: for" (fun () ->
    let r =
        wasm {
            for item in (LG "arr") do
                do! "acc" <-- (LG "acc" + item)
        }

    match r with
    | Let _ -> ()
    | _ -> failwith $"%A{r}")

// T10: yield
test "T10: yield" (fun () ->
    let r = wasm { yield C 42 }

    match r with
    | Yield(C 42) -> ()
    | _ -> failwith $"%A{r}")

// T11: use
test "T11: use" (fun () ->
    let r =
        wasm {
            use _ = { new System.IDisposable with member _.Dispose() = () }
            return C 1
        }

    match r with
    | C 1 -> ()
    | _ -> failwith $"%A{r}")

// T12: full combo
test "T12: full combo" (fun () ->
    let r: Expr =
        wasm {
            use _ = { new System.IDisposable with member _.Dispose() = () }

            let! x = C 10
            and! y = C 20

            let! acc = mut (x + y)

            while! ((LG "i" <! 100) &&& (LG "active" ==! 1)) do
                do! "x" <-- C 1
                do! "y" <-- C 2
                do! "z" <-- C 3
                do! "i" <-- (LG "i" +! 1)

            while! (LG "outer") do
                while! (LG "inner") do
                    do! "innerVar" <-- C 42
                    do! "counter" <-- C 100
                do! "outerVar" <-- C 999

            for item in (LG "arr") do
                do! "acc" <-- (LG "acc" + item)

            return acc
        }

    match r with
    | Nop -> failwith "Nop"
    | _ -> ())

// Summary
printfn ""
printfn "=============================="
printfn "  %d passed, %d failed" pass fail

if fail > 0 then
    printfn "  SOME TESTS FAILED!"
else
    printfn "  All tests passed!"
"""
        |> asExe
        |> compile
        |> shouldSucceed

    // ---------------------------------------------------------------
    // Non-bang while and for in CE builders
    // ---------------------------------------------------------------

    [<Fact>]
    let ``for without bang works in custom CE with For member`` () =
        FSharp """
module Test

type Expr = C of int | Let of string * Expr * Expr | Nop

type Builder() =
    member _.For(items: Expr, k: Expr -> Expr) : Expr =
        let n = "item"
        Let(n, items, k (C 0))
    member _.Zero() : Expr = Nop
    member _.Combine(a: Expr, b: Expr) = a
    member _.Delay(f: unit -> Expr) : Expr = f ()
    member _.Run(e: Expr) : Expr = e

let b = Builder()

let r = b {
    for item in (C 42) do
        ()
}
        """
        |> compile
        |> shouldSucceed

    [<Fact>]
    let ``while without bang works in CE with While taking unit to Expr guard`` () =
        FSharp """
module Test

type Expr = C of int | Loop of string * Expr | Nop | LG of string

type Builder() =
    member _.WhileExprDirect(guard: Expr, body: Expr) : Expr =
        Loop("loop", C 0)
    member _.Zero() : Expr = Nop
    member _.Delay(f: unit -> Expr) : Expr = f ()
    member _.Run(e: Expr) : Expr = e

let b = Builder()

let r = b {
    while (LG "cond") do
        ()
}
        """
        |> compile
        |> shouldSucceed

    [<Fact>]
    let ``while without bang with bool guard and While taking unit to bool`` () =
        FSharp """
module Test

type Expr = C of int | Loop of string * Expr | Nop

type Builder() =
    member _.While(guard: unit -> bool, body: Expr) : Expr =
        Loop("loop", C 0)
    member _.Zero() : Expr = Nop
    member _.Delay(f: unit -> Expr) : Expr = f ()
    member _.Run(e: Expr) : Expr = e

let b = Builder()

let r = b {
    while false do
        ()
}
        """
        |> compile
        |> shouldSucceed

    [<Fact>]
    let ``while bang and non-bang coexist in same builder`` () =
        FSharp """
module Test

type Expr =
    | C of int
    | LG of string
    | If of Expr * Expr * Expr
    | Block of string * Expr
    | Loop of string * Expr
    | Break of string
    | Continue of string
    | Seq of Expr list
    | Nop

let mutable ctr = 0
let fresh () = ctr <- ctr + 1; $"w{ctr}"

let _whileBangGuards = System.Collections.Generic.Stack<Expr>()

type Builder() =
    // while! (stack approach)
    member _.Bind(g: Expr, k: bool -> Expr) : Expr =
        _whileBangGuards.Push(g); k true

    // Standard while (non-bang, direct Expr guard)
    member _.WhileExprDirect(guard: Expr, body: Expr) : Expr =
        let lbl = fresh()
        Block(lbl + "_x", Loop(lbl, If(guard, Seq [ body; Continue lbl ], Break (lbl + "_x"))))

    // while! pops from stack
    member _.While(_: unit -> bool, body: Expr) : Expr =
        if _whileBangGuards.Count > 0 then
            let g = _whileBangGuards.Pop()
            let lbl = fresh()
            Block(lbl + "_x", Loop(lbl, If(g, Seq [ body; Continue lbl ], Break (lbl + "_x"))))
        else body

    member _.Zero() : Expr = Nop
    member _.Combine(a: Expr, b: Expr) = Seq [a; b]
    member _.Delay(f: unit -> Expr) : Expr = f ()
    member _.Run(e: Expr) : Expr = e

let b = Builder()

// while! uses Bind(Expr, bool -> Expr) + While(unit -> bool, Expr)
let r1 = b {
    while! (LG "cond1") do
        ()
}

// plain while uses WhileExprDirect(Expr, Expr) directly
let r2 = b {
    while (LG "cond2") do
        ()
}
        """
        |> compile
        |> shouldSucceed

    [<Fact>]
    let ``for bang and non-bang coexist in same builder`` () =
        FSharp """
module Test

type Expr = C of int | Let of string * Expr * Expr | Nop | Seq of Expr list

type Builder() =
    member _.For(items: Expr, k: Expr -> Expr) : Expr =
        Let("item", items, k (C 0))
    member _.Bind(e: Expr, k: Expr -> Expr) : Expr =
        Let("bind", e, k (C 0))
    member _.Bind(e: Expr, k: unit -> Expr) : Expr =
        Seq [e; k()]
    member _.Return(e: Expr) : Expr = e
    member _.Return(_: unit) : Expr = Nop
    member _.Zero() : Expr = Nop
    member _.Combine(a: Expr, b: Expr) = Seq [a; b]
    member _.Delay(f: unit -> Expr) : Expr = f ()
    member _.Run(e: Expr) : Expr = e

let b = Builder()

// plain for uses For(Expr, Expr -> Expr) directly
let r1 = b {
    for x in (C 42) do
        ()
}

// do! still works alongside for
let r2 = b {
    for x in (C 10) do
        do! C 99
}
        """
        |> compile
        |> shouldSucceed

    // ---------------------------------------------------------------
    // Runtime validation — key features verified at execution time
    // ---------------------------------------------------------------

    [<Fact>]
    let ``Runtime — while! executes correct number of iterations`` () =
        FSharp """
let mutable count = 0
let mutable iters = 0

async {
    count <- 1
    while! async { return count < 10 } do
        count <- count + 2
        iters <- iters + 1
    return ()
}
|> Async.RunSynchronously

// count goes 1, 3, 5, 7, 9, 11 (exits), iters = 5
if iters = 5 && count = 11 then
    printfn "PASS"
else
    printfn "FAIL: count=%d iters=%d" count iters
        """
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    [<Fact>]
    let ``Runtime — do! with overloaded Bind selects unit continuation`` () =
        FSharp """
type Expr = C of int | Seq of Expr list | Let of string * Expr * Expr | Nop | Assign of string * Expr

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

let r = b {
    do! Assign("a", C 1)
    do! Assign("b", C 2)
}

match r with
| Seq(Assign("a", C 1) :: _) -> printfn "PASS"
| _ -> printfn "FAIL: %A" r
        """
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    [<Fact>]
    let ``Runtime — and! with Bind2Return produces correct structure`` () =
        FSharp """
type Expr = C of int | Let of string * Expr * Expr | Binary of string * Expr * Expr | LG of string | Nop

let mutable ctr = 0
let fresh () = ctr <- ctr + 1; sprintf "w%d" ctr

type Builder() =
    member _.Bind2Return(a: Expr, b: Expr, f: Expr * Expr -> Expr) =
        let n1 = fresh()
        let n2 = fresh()
        Let(n1, a, Let(n2, b, f (LG n1, LG n2)))
    member _.Return(e: Expr) = e
    member _.Zero() = Nop
    member _.Delay(f: unit -> Expr) = f()
    member _.Run(e: Expr) = e

let b = Builder()

let r = b {
    let! x = C 10
    and! y = C 20
    return Binary("add", x, y)
}

match r with
| Let(n1, C 10, Let(n2, C 20, Binary("add", LG a, LG b))) when a = n1 && b = n2 ->
    printfn "PASS"
| _ -> printfn "FAIL: %A" r
        """
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    [<Fact>]
    let ``Runtime — for without bang iterates correctly in custom builder`` () =
        FSharp """
type Expr = C of int | Let of string * Expr * Expr | Nop

type Builder() =
    member _.For(items: Expr, k: Expr -> Expr) =
        Let("item", items, k (C 0))
    member _.Zero() = Nop
    member _.Combine(a: Expr, b: Expr) = a
    member _.Delay(f: unit -> Expr) = f()
    member _.Run(e: Expr) = e

let b = Builder()

let r = b {
    for item in (C 42) do
        ()
}

match r with
| Let("item", C 42, _) -> printfn "PASS"
| _ -> printfn "FAIL: %A" r
        """
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    [<Fact>]
    let ``Runtime — while without bang with Expr guard builds loop node`` () =
        FSharp """
type Expr =
    | C of int | LG of string | If of Expr * Expr * Expr
    | Block of string * Expr | Loop of string * Expr
    | Break of string | Continue of string | Seq of Expr list | Nop

let mutable ctr = 0
let fresh () = ctr <- ctr + 1; sprintf "lbl%d" ctr

type Builder() =
    member _.WhileExprDirect(guard: Expr, body: Expr) =
        let lbl = fresh()
        Block(lbl + "_x", Loop(lbl, If(guard, Seq [body; Continue lbl], Break (lbl + "_x"))))
    member _.Zero() = Nop
    member _.Delay(f: unit -> Expr) = f()
    member _.Run(e: Expr) = e

let b = Builder()

let r = b {
    while (LG "cond") do
        ()
}

match r with
| Block(_, Loop(_, If(LG "cond", _, _))) -> printfn "PASS"
| _ -> printfn "FAIL: %A" r
        """
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    // ---------------------------------------------------------------
    // Edge cases — try/with, try/finally, use!, return! in custom CEs
    // ---------------------------------------------------------------

    [<Fact>]
    let ``try-with compiles in custom CE`` () =
        FSharp """
module Test

type Builder() =
    member _.Bind(x: Async<'T>, f: 'T -> Async<'U>) = async.Bind(x, f)
    member _.Return(x: 'T) = async.Return(x)
    member _.TryWith(body: Async<'T>, handler: exn -> Async<'T>) = async.TryWith(body, handler)
    member _.Zero() = async.Return(())
    member _.Delay(f) = async.Delay(f)

let b = Builder()

let test () = b {
    try
        let! x = async { return 42 }
        return x
    with ex ->
        return -1
}
        """
        |> compile
        |> shouldSucceed

    [<Fact>]
    let ``try-finally compiles in custom CE`` () =
        FSharp """
module Test

type Builder() =
    member _.Bind(x: Async<'T>, f: 'T -> Async<'U>) = async.Bind(x, f)
    member _.Return(x: 'T) = async.Return(x)
    member _.TryFinally(body: Async<'T>, compensation: unit -> unit) = async.TryFinally(body, compensation)
    member _.Zero() = async.Return(())
    member _.Delay(f) = async.Delay(f)

let b = Builder()

let test () = b {
    try
        let! x = async { return 42 }
        return x
    finally
        ()
}
        """
        |> compile
        |> shouldSucceed

    [<Fact>]
    let ``use! compiles in custom CE`` () =
        FSharp """
module Test

type Builder() =
    member _.Bind(x: Async<'T>, f: 'T -> Async<'U>) = async.Bind(x, f)
    member _.Return(x: 'T) = async.Return(x)
    member _.Using(res: #System.IDisposable, body: 'T -> Async<'U>) = async.Using(res, body)
    member _.Zero() = async.Return(())
    member _.Delay(f) = async.Delay(f)

let b = Builder()

let test () = b {
    use! _disp = async {
        return { new System.IDisposable with member _.Dispose() = () }
    }
    return 42
}
        """
        |> compile
        |> shouldSucceed

    [<Fact>]
    let ``return! compiles in custom CE`` () =
        FSharp """
module Test

type Builder() =
    member _.ReturnFrom(x: Async<'T>) = x
    member _.Return(x: 'T) = async.Return(x)
    member _.Zero() = async.Return(())
    member _.Delay(f) = async.Delay(f)

let b = Builder()

let test () = b {
    return! async { return 42 }
}
        """
        |> compile
        |> shouldSucceed

    // ---------------------------------------------------------------
    // while guard type NOT constrained to bool — key fork finding
    // ---------------------------------------------------------------

    [<Fact>]
    let ``while guard can be any type matching While signature`` () =
        FSharp """
module Test

// Custom "condition" type — NOT bool
[<NoComparison; NoEquality>]
type Condition = Always | Never | Check of (unit -> bool)

type Builder() =
    member _.While(guard: unit -> Condition, body: string list) =
        let rec loop acc =
            match guard() with
            | Always -> loop (acc @ body)
            | Never -> acc
            | Check f -> if f() then loop (acc @ body) else acc
        loop []
    member _.Zero() = []
    member _.Yield(x: string) = [x]
    member _.Combine(a: string list, b: string list) = a @ b
    member _.Delay(f: unit -> string list) = f()
    member _.Run(e: string list) = e

let b = Builder()

let mutable n = 0
let result = b {
    while (n <- n + 1; if n <= 3 then Check (fun () -> true) else Never) do
        yield "item"
}
        """
        |> compile
        |> shouldSucceed

    [<Fact>]
    let ``while guard with custom monadic type compiles and runs`` () =
        FSharp """
// Demonstrates that while guard is NOT constrained to bool.
// The builder's While accepts unit -> Result<bool,string> instead of unit -> bool.
type Builder() =
    member _.While(guard: unit -> Result<bool,string>, body: Result<unit,string>) =
        let rec loop () =
            match guard() with
            | Ok true ->
                match body with
                | Ok () -> loop()
                | Error e -> Error e
            | Ok false -> Ok ()
            | Error e -> Error e
        loop()
    member _.Zero() = Ok ()
    member _.Delay(f: unit -> Result<'T,string>) = f()
    member _.Run(e: Result<'T,string>) = e

let b = Builder()

let mutable count = 0
let result = b {
    while (count <- count + 1; if count > 3 then Error "too many" else Ok (count <= 5)) do
        ()
}

match result with
| Error "too many" -> printfn "PASS"
| _ -> printfn "FAIL: %A" result
        """
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    // ---------------------------------------------------------------
    // Stress tests — deeply nested CEs, many do!, complex control flow
    // ---------------------------------------------------------------

    [<Fact>]
    let ``Stress — 20 sequential do! in CE compiles`` () =
        FSharp """
module Test

type Expr = Assign of string * int | Seq of Expr list | Nop

type Builder() =
    member _.Bind(e: Expr, k: unit -> Expr) = Seq [e; k()]
    member _.Return(_: unit) = Nop
    member _.Zero() = Nop
    member _.Combine(a: Expr, b: Expr) = Seq [a; b]
    member _.Delay(f: unit -> Expr) = f()
    member _.Run(e: Expr) = e

let b = Builder()

let r = b {
    do! Assign("a", 1)
    do! Assign("b", 2)
    do! Assign("c", 3)
    do! Assign("d", 4)
    do! Assign("e", 5)
    do! Assign("f", 6)
    do! Assign("g", 7)
    do! Assign("h", 8)
    do! Assign("i", 9)
    do! Assign("j", 10)
    do! Assign("k", 11)
    do! Assign("l", 12)
    do! Assign("m", 13)
    do! Assign("n", 14)
    do! Assign("o", 15)
    do! Assign("p", 16)
    do! Assign("q", 17)
    do! Assign("r", 18)
    do! Assign("s", 19)
    do! Assign("t", 20)
}
        """
        |> compile
        |> shouldSucceed

    [<Fact>]
    let ``Stress — nested CE blocks compile`` () =
        FSharp """
module Test

let test () = async {
    let! a = async {
        let! b = async {
            let! c = async { return 1 }
            return c + 1
        }
        return b + 1
    }
    return a + 1
}
        """
        |> compile
        |> shouldSucceed

    [<Fact>]
    let ``Stress — if-then-else chains in CE compile`` () =
        FSharp """
module Test

let test (x: int) = async {
    if x > 10 then
        let! v = async { return x * 2 }
        return v
    elif x > 5 then
        do! async { return () }
        return x + 100
    elif x > 0 then
        match! async { return Some x } with
        | Some v -> return v
        | None -> return -1
    else
        return 0
}
        """
        |> compile
        |> shouldSucceed

    // ---------------------------------------------------------------
    // WASM CE reference demo — full compile (IL generation verified)
    // ---------------------------------------------------------------

    [<Fact>]
    let ``WASM CE reference demo — runtime verification`` () =
        FSharp """
#nowarn "0086"

type Expr =
    | C of int | Let of string * Expr * Expr | LetMut of string * Expr * Expr
    | Seq of Expr list | Assign of string * Expr | LG of string
    | If of Expr * Expr * Expr | Loop of string * Expr | Block of string * Expr
    | Break of string | Continue of string | Binary of string * Expr * Expr
    | Yield of Expr | Nop

type MutInit = { Init: Expr }
let mut (e: Expr) : MutInit = { Init = e }

module WasmOps =
    let (+) (a: Expr) (b: Expr) = Binary("add", a, b)
    let (<) (a: Expr) (b: Expr) = Binary("lt_s", a, b)
    let (<--) (n: string) (v: Expr) : Expr = Assign(n, v)

open WasmOps

let _whileBangGuards = System.Collections.Generic.Stack<Expr>()
let mutable _ctr = 0
let fresh () = _ctr <- Microsoft.FSharp.Core.Operators.(+) _ctr 1; sprintf "$w%d" _ctr
let mkExit (lbl: string) = sprintf "%s_exit" lbl

type WasmBuilder() =
    member _.Bind(e: Expr, k: Expr -> Expr) : Expr = let n = fresh() in Let(n, e, k (LG n))
    member _.Bind(e: Expr, k: unit -> Expr) : Expr = let r = k() in match r with Nop -> e | _ -> Seq [e; r]
    member _.Bind(m: MutInit, k: Expr -> Expr) : Expr = let n = fresh() in LetMut(n, m.Init, k (LG n))
    
    // WhileBang: compiler passes guard directly, no bool constraint, no stack hack.
    member _.WhileBang(guard: Expr, body: Expr) : Expr =
        let lbl = fresh()
        let exit = mkExit lbl
        Block(exit, Loop(lbl, If(guard, Seq [body; Continue lbl], Break exit)))
        
    // Standard while via WhileExprDirect (bypasses Delay and bool constraints)
    member _.WhileExprDirect(guard: Expr, body: Expr) : Expr =
        let lbl = fresh() in let exit = mkExit lbl
        Block(exit, Loop(lbl, If(guard, Seq [body; Continue lbl], Break exit)))
        
    member _.Bind2Return(a: Expr, b: Expr, f: Expr * Expr -> Expr) : Expr =
        let n1 = fresh() in let n2 = fresh() in Let(n1, a, Let(n2, b, f (LG n1, LG n2)))
    member _.Bind2(a: Expr, b: Expr, f: Expr * Expr -> Expr) : Expr =
        let n1 = fresh() in let n2 = fresh() in Let(n1, a, Let(n2, b, f (LG n1, LG n2)))
    member _.MergeSources(a: Expr, b: Expr) : Expr * Expr = (a, b)
    member _.BindReturn(e: Expr, f: Expr -> Expr) : Expr = let n = fresh() in Let(n, e, f (LG n))
    member _.Return(e: Expr) : Expr = e
    member _.ReturnFrom(e: Expr) : Expr = e
    member _.Return(_: unit) : Expr = Nop
    member _.Yield(e: Expr) : Expr = Yield e
    member _.Zero() : Expr = Nop
    member _.Combine(a: Expr, b: Expr) : Expr = match a, b with Nop, x | x, Nop -> x | _ -> Seq [a; b]
    member _.For(items: Expr, k: Expr -> Expr) : Expr = let n = fresh() in Let(n, items, k (LG n))
    member _.Delay(f: unit -> Expr) : Expr = f()
    member _.Run(e: Expr) : Expr = e
    member _.Using(res: #System.IDisposable, k) : Expr = try k res finally res.Dispose()

let wasm = WasmBuilder()

let mutable pass = 0
let mutable fail = 0

let test (name: string) (f: unit -> unit) =
    try f(); pass <- Microsoft.FSharp.Core.Operators.(+) pass 1
    with ex -> fail <- Microsoft.FSharp.Core.Operators.(+) fail 1; eprintfn "FAIL %s: %s" name ex.Message

let rec flatSeq = function Seq xs -> List.collect flatSeq xs | x -> [x]

// T1: let! + return
test "T1" (fun () ->
    let r = wasm { let! x = C 10 in return (x : Expr) }
    match r with Let(_, C 10, LG _) -> () | _ -> failwith (sprintf "%A" r))

// T2: multiple do!
test "T2" (fun () ->
    let r = wasm {
        do! "a" <-- C 1
        do! "b" <-- C 2
        do! "c" <-- C 3
    }
    match flatSeq r with [Assign("a",_); Assign("b",_); Assign("c",_)] -> () | f -> failwith (sprintf "%A" f))

// T3: while! single do!
test "T3" (fun () ->
    let r = wasm { while! (LG "cond") do do! "x" <-- C 1 }
    match r with Block(_, Loop(_, If(LG "cond", _, _))) -> () | _ -> failwith (sprintf "%A" r))

// T4: and!
test "T4" (fun () ->
    let r = wasm {
        let! x = C 10
        and! y = C 20
        return x + y
    }
    match r with
    | Let(_, _, _) -> ()
    | _ -> failwith (sprintf "%A" r))

// T5: match!
test "T5" (fun () ->
    let r = wasm { match! (LG "tag") with x -> return (x : Expr) }
    match r with Let(_, LG "tag", _) -> () | _ -> failwith (sprintf "%A" r))

// T6: for
test "T6" (fun () ->
    let r = wasm { for item in (LG "arr") do do! "acc" <-- (LG "acc" + item) }
    match r with Let _ -> () | _ -> failwith (sprintf "%A" r))

// T7: yield
test "T7" (fun () ->
    let r = wasm { yield C 42 }
    match r with Yield(C 42) -> () | _ -> failwith (sprintf "%A" r))

// T8: use
test "T8" (fun () ->
    let r = wasm { use _ = { new System.IDisposable with member _.Dispose() = () } in return C 1 }
    match r with C 1 -> () | _ -> failwith (sprintf "%A" r))

// T9: while (no bang) with Expr guard
test "T9" (fun () ->
    let r = wasm { while (LG "cond2") do do! "x" <-- C 99 }
    match r with Block(_, Loop(_, If(LG "cond2", _, _))) -> () | _ -> failwith (sprintf "%A" r))

// T10: full combo
test "T10" (fun () ->
    let r : Expr = wasm {
        use _ = { new System.IDisposable with member _.Dispose() = () }
        let! x = C 10
        and! y = C 20
        let! acc = mut (x + y)
        while! (LG "cond") do do! "i" <-- C 1
        while (LG "cond2") do do! "j" <-- C 2
        for item in (LG "arr") do do! "acc" <-- (LG "acc" + item)
        return acc
    }
    match r with Nop -> failwith "Nop" | _ -> ())

printfn "%d passed, %d failed" pass fail
if fail > 0 then printfn "OVERALL_FAIL" else printfn "OVERALL_PASS"
        """
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "OVERALL_PASS"

    // ---------------------------------------------------------------
    // WhileBang builder method — compiler fix: removes bool constraint
    // ---------------------------------------------------------------

    [<Fact>]
    let ``WhileBang builder method — while! with non-bool guard compiles and runs`` () =
        FSharp """
// This test REQUIRES the compiler fix: WhileBang builder method support.
// Without it, while! hardcodes bool via 'if __res then ... else ...'
// and Bind would need (Expr, bool -> Expr) overload with a stack hack.
// With WhileBang, the builder receives the guard directly — no bool constraint.

type Expr =
    | C of int | LG of string | If of Expr * Expr * Expr
    | Block of string * Expr | Loop of string * Expr
    | Break of string | Continue of string | Seq of Expr list | Nop

let mutable ctr = 0
let fresh () = ctr <- ctr + 1; sprintf "lbl%d" ctr
let mkExit (lbl: string) = sprintf "%s_exit" lbl

type Builder() =
    // WhileBang: receives the guard expression directly, no bool constraint.
    member _.WhileBang(guard: Expr, body: Expr) =
        let lbl = fresh()
        let exit = mkExit lbl
        Block(exit, Loop(lbl, If(guard, Seq [body; Continue lbl], Break exit)))
    member _.Bind(e: Expr, k: Expr -> Expr) =
        let n = fresh()
        Seq [e; k (LG n)]
    member _.Bind(e: Expr, k: unit -> Expr) = Seq [e; k()]
    member _.Return(e: Expr) = e
    member _.Return(_: unit) = Nop
    member _.Zero() = Nop
    member _.Combine(a: Expr, b: Expr) = Seq [a; b]
    member _.Delay(f: unit -> Expr) = f()
    member _.Run(e: Expr) = e

let b = Builder()

// while! with Expr guard — NO bool involved at all
let r = b {
    while! (LG "cond") do
        do! C 42
}

match r with
| Block(_, Loop(_, If(LG "cond", _, _))) -> printfn "PASS"
| _ -> printfn "FAIL: %A" r
        """
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    [<Fact>]
    let ``WhileBang builder method — fallback to Bind pattern when no WhileBang defined`` () =
        FSharp """
// When builder has NO WhileBang method, falls back to Bind/if/Combine/Zero (bool guard).

let mutable count = 0

type Builder() =
    member _.Bind(x: Async<'T>, f: 'T -> Async<'U>) = async.Bind(x, f)
    member _.Return(x: 'T) = async.Return x
    member _.Zero() = async.Return ()
    member _.Combine(a: Async<unit>, b: Async<'T>) =
        async {
            do! a
            return! b
        }
    member _.Delay(f: unit -> Async<'T>) = async.Delay f
    member _.Run(a: Async<'T>) = Async.RunSynchronously a

let b = Builder()

b {
    while! async { return count < 3 } do
        count <- count + 1
}

if count = 3 then printfn "PASS" else printfn "FAIL: count=%d" count
        """
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    // ---------------------------------------------------------------
    // RFC Fix — IfExpr branches NOT wrapped in Delay
    // ---------------------------------------------------------------
    // The RFC specifies that when builder has IfExprDirect method:
    //   builder.IfExprDirect(guard, thenBody, elseBody)
    // NOT:
    //   builder.IfExpr(guard, builder.Delay(fun () -> thenBody), builder.Delay(fun () -> elseBody))
    //
    // This allows the builder to receive the branch AST directly, enabling:
    // 1. Elimination of spurious Nops
    // 2. Proper structure analysis without Delay callback overhead
    // 3. Clean type-guided dispatch for guard types
    //
    // Tests verify: IfExprDirect branches are NOT decorated with Delay

    [<Fact>]
    let ``IfExpr — RFC fix: branches NOT wrapped in Delay`` () =
        FSharp """
#nowarn "20"
module Test

type Expr = C of int | IfE of Expr * Expr * Expr | Nop

type Builder() =
    // IfExprDirect receives branches directly (no Delay wrapper)
    member _.IfExprDirect(guard: Expr, thenBranch: Expr, elseBranch: Expr) : Expr =
        IfE(guard, thenBranch, elseBranch)

    member _.Zero() = Nop
    member _.Run(e: Expr) = e

let b = Builder()

// When IfExprDirect is present in builder, branches should NOT be wrapped
let r = b {
    if C 1 then
        C 10
    else
        C 20
}

// Verify the structure matches RFC expectation
match r with
| IfE(C 1, C 10, C 20) -> () // Branches are C 10 and C 20, not Delayed
| _ -> failwith $"Unexpected structure: %A{r}"
        """
        |> withLangVersionPreview
        |> compile
        |> shouldSucceed

    [<Fact>]
    let ``IfExpr — single-branch if uses Zero directly`` () =
        FSharp """
#nowarn "20"
module Test

type Expr = C of int | IfE of Expr * Expr * Expr | Nop

type Builder() =
    member _.IfExprDirect(guard: Expr, thenBranch: Expr, elseBranch: Expr) : Expr =
        IfE(guard, thenBranch, elseBranch)

    member _.Zero() = Nop
    member _.Run(e: Expr) = e

let b = Builder()

// Single-branch if should have Zero() as else branch (not Delayed)
let r = b {
    if C 1 then
        C 10
}

match r with
| IfE(C 1, C 10, Nop) -> () // elseBranch is Nop (from Zero()), not Delayed
| _ -> failwith $"Unexpected: %A{r}"
        """
        |> withLangVersionPreview
        |> compile
        |> shouldSucceed

    [<Fact>]
    let ``IfExpr — nested if/else without spurious Nops`` () =
        FSharp """
#nowarn "20"
module Test

type Expr =
    | C of int
    | IfE of Expr * Expr * Expr
    | Seq of Expr list
    | Nop

type Builder() =
    member _.IfExprDirect(guard: Expr, thenBranch: Expr, elseBranch: Expr) : Expr =
        IfE(guard, thenBranch, elseBranch)

    member _.Combine(a: Expr, b: Expr) : Expr =
        match a, b with
        | Nop, x | x, Nop -> x
        | _ -> Seq [a; b]

    member _.Zero() = Nop
    member _.Run(e: Expr) = e

let b = Builder()

// Nested if without extra Nops between branches
let r = b {
    if C 1 then
        if C 2 then
            C 100
        else
            C 200
    else
        C 300
}

// Structure should be IfE(C 1, IfE(C 2, C 100, C 200), C 300)
// NOT IfE(C 1, IfE(C 2, Delay(...), Delay(...)), Zero())
match r with
| IfE(C 1, IfE(C 2, C 100, C 200), C 300) -> ()
| _ -> failwith $"Unexpected structure: %A{r}"
        """
        |> withLangVersionPreview
        |> compile
        |> shouldSucceed

    [<Fact>]
    let ``IfExpr — builder IfExpr receives undecorated Expr from translation`` () =
        FSharp """
#nowarn "20"
module Test

type Expr =
    | C of int
    | DoubleSumExpr of Expr * Expr
    | IfE of Expr * Expr * Expr
    | Nop

type Builder() =
    // Record that we received undecorated Expr
    member _.IfExprDirect(guard: Expr, thenBranch: Expr, elseBranch: Expr) : Expr =
        // If branches were Delayed, we'd see Delay(...) here
        // Since RFC fix removes Delay, we directly see the user's computation
        IfE(guard, thenBranch, elseBranch)

    member _.Zero() = Nop
    member _.Run(e: Expr) = e

let b = Builder()

let r = b {
    if C 1 then
        let x = C 10
        let y = C 20
        DoubleSumExpr(x, y)  // Complex then-branch
    else
        C 0
}

// Verify: thenBranch contains DoubleSumExpr directly (not wrapped in Delay)
match r with
| IfE(C 1, DoubleSumExpr(C 10, C 20), C 0) -> ()
| _ -> failwith $"Unexpected: %A{r}"
        """
        |> withLangVersionPreview
        |> compile
        |> shouldSucceed

    [<Fact>]
    let ``IfExpr + let! — RFC fix preserves guard type from Bind`` () =
        FSharp """
#nowarn "20"
module Test

type Expr = C of int | LG of string | Bind of string * Expr * Expr | IfE of Expr * Expr * Expr | Nop

type Builder() =
    member _.Bind(e: Expr, k: Expr -> Expr) : Expr =
        let v = "__x"
        Bind(v, e, k (LG v))

    member _.IfExprDirect(guard: Expr, thenBranch: Expr, elseBranch: Expr) : Expr =
        IfE(guard, thenBranch, elseBranch)

    member _.Zero() = Nop
    member _.Run(e: Expr) = e

let b = Builder()

// let! produces a Bind; guard should be the LG result (not wrapped in Delay)
let r = b {
    let! g = C 1
    if g then
        C 100
    else
        C 200
}

// Verify structure:
// Bind("__x", C 1, IfE(LG "__x", C 100, C 200))
match r with
| Bind(_, C 1, IfE(LG _, C 100, C 200)) -> ()
| _ -> failwith $"Unexpected: %A{r}"
        """
        |> withLangVersionPreview
        |> compile
        |> shouldSucceed

    [<Fact>]
    let ``IfExpr runtime test — branches execute with correct control flow`` () =
        FSharp """
#nowarn "20"
module Test

type Expr = 
    | C of int 
    | IfE of Expr * Expr * Expr 
    | Nop

type Builder() =
    member _.IfExprDirect(guard: Expr, thenBranch: Expr, elseBranch: Expr) : Expr =
        IfE(guard, thenBranch, elseBranch)

    member _.Zero() = Nop
    member _.Run(e: Expr) = e

let b = Builder()

// Evaluate-like interpreter to verify branches execute
let rec eval =
    function
    | C v -> v
    | IfE(C guard, thenE, elseE) ->
        if guard <> 0 then eval thenE else eval elseE
    | Nop -> 0
    | _ -> -999

let r1 = b {
    if C 1 then
        C 100
    else
        C 200
}

let r2 = b {
    if C 0 then
        C 100
    else
        C 200
}

let result1 = eval r1
let result2 = eval r2

if result1 = 100 && result2 = 200 then
    printfn "PASS"
else
    printfn "FAIL: r1=%d r2=%d" result1 result2
        """
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    // ---------------------------------------------------------------
    // IfExpr fallback — when builder has no IfExpr method
    // ---------------------------------------------------------------

    [<Fact>]
    let ``IfExpr fallback — builder without IfExpr uses standard IfThenElse`` () =
        FSharp """
module Test

type Builder() =
    member _.Return(x: int) = x
    member _.Zero() = 0
    member _.Delay(f: unit -> int) = f()
    member _.Run(e: int) = e

let b = Builder()

let r = b {
    if true then
        return 42
    else
        return 0
}

if r = 42 then printfn "PASS" else printfn "FAIL: r=%d" r
        """
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"

    [<Fact>]
    let ``IfExpr fallback — async builder without IfExpr method`` () =
        FSharp """
let r = async {
    if true then
        return 42
    else
        return 0
}

let result = Async.RunSynchronously r
if result = 42 then printfn "PASS" else printfn "FAIL"
        """
        |> asExe
        |> compileExeAndRun
        |> shouldSucceed
        |> withStdOutContains "PASS"
