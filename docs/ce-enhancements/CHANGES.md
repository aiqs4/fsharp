# CE Enhancements — Change Log

## BindReturn poisons `do!` Fix

**File:** `src/Compiler/Checking/Expressions/CheckComputationExpressions.fs`
**Function:** `TranslateComputationExpressionBind`

**Before:** When a builder defined `BindReturn`, the final `do!` in a CE block was desugared to
`let! () = e in return ()` and then optimized to `BindReturn(e, fun () -> ())`. The `unit -> unit`
continuation could not match `BindReturn(M, M -> M)`, causing FS0001.

**After:** When `consumePat` is a unit pattern (from `do!` desugaring), the `BindReturn` optimization
is skipped. The compiler falls back to `Bind(e, fun () -> Return(()))`, which correctly selects
the unit-continuation `Bind` overload.

---

## `if!` Token and Desugaring

**Files:**
- `src/Compiler/lex.fsl` — Added `IF_BANG` token rule
- `src/Compiler/pars.fsy` — Added `IF_BANG` grammar rules; desugars to `MatchBang` with `true`/`false` clauses
- `src/Compiler/SyntaxTree/LexFilter.fs` — Handle `IF_BANG` in context management
- `src/Compiler/Service/ServiceLexing.fs` / `.fsi` — Added `IfBang` token kind
- `src/Compiler/Facilities/LanguageFeatures.fs` / `.fsi` — Added `LanguageFeature.IfBang` (preview)
- `src/Compiler/FSComp.txt` — Added `featureIfBang` string

**Desugaring:**
```
if! guard then thenBranch else elseBranch
  → match! guard with true -> thenBranch | false -> elseBranch
```
Reuses existing `MatchBang` / `Bind + match` CE machinery. No new AST nodes.

---

## `for!` Token and Desugaring

**Files:** Same set as `if!` above.

**Desugaring:**
```
for! pat in collection do body
  → builder.For(collection, fun pat -> body)
```
Uses `ForEach` with `isFromSource=false` to skip `Source()` wrapping, removing the `IEnumerable` constraint.

---

## `while!` Carrier-Free Desugaring

**File:** `src/Compiler/Checking/Expressions/CheckComputationExpressions.fs`
**Function:** `TryTranslateComputationExpression`, `SynExpr.WhileBang` case

**Before:** Desugared to `let! first = guard; let mutable cond = first; while cond do body; let! first = guard; cond <- first`.
Required `While(unit -> bool, M<unit>)` on the builder.

**After:** Desugars to recursive `Bind/Combine/Zero`:
```
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
Only requires `Bind`, `Combine`, and `Zero`. `While` is no longer required for `while!`.
Regular `while` (non-bang) is unchanged.
