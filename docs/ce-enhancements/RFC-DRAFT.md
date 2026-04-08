# RFC FS-CENH — Computation Expression Enhancements

- [x] Implementation complete
- [x] 40 tests passing (compile + runtime verified)
- [x] 87 existing CE tests + 4 WhileBang tests — no regressions
- [ ] Reviewed by F# team

## Summary

This RFC proposes five enhancements to F# Computation Expressions (CEs) that improve expressiveness for DSL-style builders (e.g., WASM code generators, AST builders) while maintaining full backward compatibility with existing async/task/seq CEs.

## Motivation

F# CEs are a powerful metaprogramming facility, but several limitations prevent their use as general-purpose DSL builders:

1. **`if!` and `for!` are reserved but unusable** — The compiler reserves these tokens (FS1141) but provides no desugaring, forcing workarounds like `match! guard with true -> ... | false -> ...` instead of `if! guard then ... else ...`.

2. **`while!` hardcodes `bool`** — The current `while!` desugaring generates `if __res then ... else ...`, forcing the Bind continuation to produce `bool`. DSL builders that want non-bool guard types (e.g., WASM Expr) must use fragile hacks (side-effecting `Stack<Expr>` to tunnel values through the bool bottleneck).

3. **`BindReturn` poisons `do!`** — When a builder defines `BindReturn(M<'T>, 'T -> M<'U>)`, the final `do!` is incorrectly optimized to `BindReturn(e, fun () -> ())`, causing FS0001 type errors.

4. **`for` inside CEs forces `IEnumerable` via `Source()`** — When a builder defines `Source()`, regular `for` wraps the collection through it, requiring `IEnumerable`. Builders working with custom collection types can't use `for` without also defining `Source`.

## Detailed Design

### 1. `if!` — Conditional Bind

**Syntax:**
```fsharp
builder {
    if! asyncCondition then
        doSomething()
    else
        doOther()
}
```

**Desugaring:** Reuses existing `match!` infrastructure:
```fsharp
match! asyncCondition with
| true -> doSomething()
| false -> doOther()
```

No new AST nodes. The parser transforms `if!` into `SynExpr.MatchBang` with `true`/`false` pattern clauses.

**Required builder methods:** Same as `match!` — `Bind`.

**Language feature gate:** `LanguageFeature.IfBang` (preview).

### 2. `for!` — Source-Free Iteration

**Syntax:**
```fsharp
builder {
    for! item in customCollection do
        processItem item
}
```

**Desugaring:** Same as `for`, but with `isFromSource = false` to skip `Source()` wrapping:
```fsharp
builder.For(customCollection, fun item -> processItem item)
```

This allows builders to define `For` with arbitrary collection types without requiring `IEnumerable`.

**Required builder methods:** `For`.

**Language feature gate:** `LanguageFeature.ForBang` (preview).

### 3. `while!` — Carrier-Free Loop with Optional `WhileBang`

#### 3a. Carrier-Free Desugaring (existing improvement)

**Before (upstream):** `while!` required `While(unit -> bool, M<unit>)` on the builder, limiting it to bool-typed guards.

**After:** Desugars to recursive `Bind/Combine/Zero`:
```fsharp
while! guard do body
  →
let rec __whileLoop () =
    builder.Bind(guard, fun __res ->
        if __res then
            builder.Combine(translatedBody, builder.Delay(fun () -> __whileLoop()))
        else
            builder.Zero())
__whileLoop()
```

Only requires `Bind`, `Combine`, `Zero`. Works for async/task CEs where guard is `Async<bool>`.

#### 3b. `WhileBang` Builder Method (new)

**Problem:** The `if __res then` in 3a hardcodes `bool`. DSL builders where `Bind` extracts non-bool types cannot use `while!`.

**Solution:** If the builder defines a `WhileBang` method, the compiler uses it directly:
```fsharp
while! guard do body
  → builder.WhileBang(guard, builder.Delay(fun () -> body))
```

The guard passes directly to the builder — no `Bind`, no bool coercion. The builder's `WhileBang` signature determines the guard type.

**Fallback:** If no `WhileBang` method exists, the compiler uses the Bind/if pattern from 3a. Fully backward compatible.

**Example — WASM code generator:**
```fsharp
type WasmBuilder() =
    // Guard is Expr, not bool. No stack hack needed.
    member _.WhileBang(guard: Expr, body: Expr) =
        Block(exit, Loop(lbl, If(guard, Seq [body; Continue lbl], Break exit)))
    member _.Delay(f: unit -> Expr) = f()
    // ... other members

let wasm = WasmBuilder()
let result = wasm {
    while! (LG "cond") do    // guard is Expr, not Async<bool>
        do! Assign("x", C 1)
}
// result: Block(exit, Loop(lbl, If(LG "cond", Seq [...], Break exit)))
```

### 4. `BindReturn`/`do!` Fix

**Problem:** When a builder defines `BindReturn(M<'T>, 'T -> M<'U>)`, the compiler optimizes `let! x = e in return x` to `BindReturn(e, fun x -> x)`. But `do!` desugars to `let! () = e in return ()`, which becomes `BindReturn(e, fun () -> ())`. The `unit -> unit` continuation doesn't match `'T -> M<'U>`, causing FS0001.

**Fix:** When the `consumePat` is a unit pattern (from `do!` desugaring), the `BindReturn` optimization is skipped. The compiler falls back to `Bind(e, fun () -> Return(()))`, which correctly selects the unit-continuation `Bind` overload.

### 5. FS0041 Overload Resolution Fix

When a builder has both `Bind(M<'T>, 'T -> M<'R>)` and `Bind(M<'T>, unit -> M<'R>)`, `do!` now correctly selects the unit-continuation overload without ambiguity. The fix annotates the synthetic continuation `fun () -> ...` with explicit `unit` type, giving the constraint solver enough information to resolve the overload.

## Backward Compatibility

All changes are fully backward compatible:

| Change | Impact on existing code |
|--------|------------------------|
| `if!` | Previously a parse error (FS1141). No existing code uses it. |
| `for!` | Previously a parse error (FS1141). No existing code uses it. |
| `while!` carrier-free | Existing builders with `While` still work. New desugaring only requires `Bind`/`Combine`/`Zero`. |
| `WhileBang` method | Optional. Builders without it use existing Bind/if fallback. |
| `BindReturn`/`do!` fix | Only affects builders with both `BindReturn` and overloaded `Bind`. Previously caused FS0001. |
| FS0041 fix | Only affects builders with overloaded `Bind`. Previously caused FS0041. |

## Test Matrix

| Test Category | Count | Method |
|---------------|-------|--------|
| BindReturn poisoning | 2 | compile |
| `if!` | 4 | compile |
| `for!` | 2 | compile |
| `while!` | 2 | compile |
| Regression (async/task/seq) | 3 | compile |
| Full AST builder | 2 | compile |
| FS0041 overload | 2 | compile |
| Non-bang while/for | 3 | compile |
| Bang/non-bang coexistence | 2 | compile |
| Runtime — while! iterations | 1 | compileExeAndRun |
| Runtime — do! overload | 1 | compileExeAndRun |
| Runtime — and! Bind2Return | 1 | compileExeAndRun |
| Runtime — for without bang | 1 | compileExeAndRun |
| Runtime — while Expr guard | 1 | compileExeAndRun |
| try/with, try/finally, use!, return! | 4 | compile |
| While guard type freedom | 2 | compile + run |
| Stress tests | 3 | compile |
| WASM CE reference demo (10 subtests) | 2 | compileExeAndRun |
| WhileBang builder method | 2 | compileExeAndRun |
| WhileBang fallback | 1 | compileExeAndRun |
| **Total** | **40** | |

## Implementation

All changes are in `src/Compiler/Checking/Expressions/CheckComputationExpressions.fs` (CE desugaring), `src/Compiler/pars.fsy` (parser), `src/Compiler/lex.fsl` (lexer), and supporting infrastructure files.

See [CHANGES.md](CHANGES.md) for detailed implementation notes.
