**Core: stamp-based incrementalism**

Each step writes a stamp file to `artifacts/.stamps/<name>` on success. The `stale()` function checks: is `FORCE=true`? Stamp missing? Any matching file under the scoped root newer than the stamp? If any yes → rebuild, else skip. This is mtime-based (no SHA overhead) and reliable for a local dev loop.

```bash
# stale <stamp_name> <search_root> <name_patterns...>
stale "compiler" "$REPO_ROOT/src" "*.fs" "*.fsi" "*.fsproj" ...
```

The search root is deliberately **scoped per step** — a change in `tests/` does not invalidate the `compiler` stamp, and vice versa. That's the critical difference from naive "if any file changed, rebuild everything."

---

**Restore once, `--no-restore` everywhere**

`step_restore` runs `dotnet restore` against the solution once, stamps it, and every downstream `build`/`pack`/`publish` call passes `--no-restore`. Bootstrap is the only exception — it has its own NuGet graph via `proto.proj` and embeds its own restore intentionally.

---

**`FSHARP_TARGETS_PATH` bug fix**

The original pointed to the `.Targets` file:
```bash
export FSHARP_TARGETS_PATH="$BOOTSTRAP/fsc/Microsoft.FSharp.Targets"
```

MSBuild's `Microsoft.NET.Sdk.FSharp.props` line 29 treats this env var as a **directory base** and calls `NormalizePath($(FSHARP_TARGETS_PATH), 'Microsoft.FSharp.NetSdk.props')`. Pointing to the file makes that resolve to a non-existent path, causing the XML parse error you're seeing. Fixed to:
```bash
export FSHARP_TARGETS_PATH="$BOOTSTRAP/fsc"
```

---

**Surgical step re-runs**

```bash
build.sh --step compiler -f -v      # Force only the compiler step
build.sh --step tests-run           # Re-run tests, no rebuild
build.sh --clean-step bootstrap     # Invalidate just the bootstrap stamp
build.sh --clean-stamps             # Nuke all stamps → next run is full
```

Each step is a standalone callable function — no hidden sequencing dependencies when invoked directly.

---

**Parallel test builds**

Pass `-j N` and `step_build_tests` fans out background jobs with per-job temp logs. Failures are collected and all logs dumped before the fatal exit — no silent partial failures:

```bash
build.sh --test -j 4
```
