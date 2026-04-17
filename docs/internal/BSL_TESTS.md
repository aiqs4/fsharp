Summary of *.bsl tests + framework usage

I scanned the repo for .bsl files and how they’re referenced, then mapped the framework-specific ones. Here’s what I found:

1) Baseline frameworks in test harness code
- tests/FSharp.Test.Utilities/Compiler.fs
  - IL baselines are chosen per target framework at runtime:
    - NETCOREAPP => “.il.netcore[.debug|.release].bsl” and “.il.netcore.bsl”
    - non-NETCOREAPP => “.il.net472[.debug|.release].bsl” and “.il.net472.bsl”
  - F# error baselines are “.err.bsl”

- tests/FSharp.Core.UnitTests/SurfaceArea.fs
  - NETCOREAPP => platform “netstandard21”
  - non-NETCOREAPP => platform “netstandard20”
  - baseline names: FSharp.Core.SurfaceArea.{platform}.{flavor}.bsl

- tests/FSharp.Compiler.Service.Tests/SurfaceArea.fs
  - platform hard-coded to “netstandard21”
  - baseline names: FSharp.Compiler.Service.SurfaceArea.{platform}.bsl

2) Framework-tagged baselines discovered (paths)
- Component tests IL baselines: net472 + netcore
  - tests/FSharp.Compiler.ComponentTests/**/…il.net472*.bsl
  - tests/FSharp.Compiler.ComponentTests/**/…il.netcore*.bsl
  - These are used via the NETCOREAPP/!NETCOREAPP conditional in Compiler.fs.

- Surface area baselines
  - tests/FSharp.Compiler.Service.Tests/FSharp.Compiler.Service.SurfaceArea.netstandard20.bsl (legacy)
  - tests/FSharp.Compiler.Service.Tests/FSharp.Compiler.Service.SurfaceArea.netstandard21.bsl (expected per SurfaceArea.fs)
  - tests/FSharp.Core.UnitTests/FSharp.Core.SurfaceArea.netstandard20.{debug|release}.bsl
  - tests/FSharp.Core.UnitTests/FSharp.Core.SurfaceArea.netstandard21.{debug|release}.bsl

- ILVerify baselines
  - tests/ILVerify/ilverify_FSharp.Compiler.Service_{Debug|Release}_netstandard2.0.bsl
  - tests/ILVerify/ilverify_FSharp.Compiler.Service_{Debug|Release}_net10.0.bsl
  - tests/ILVerify/ilverify_FSharp.Core_{Debug|Release}_netstandard2.0.bsl
  - tests/ILVerify/ilverify_FSharp.Core_{Debug|Release}_netstandard2.1.bsl

- Other .bsl files
  - tests/service/data/**: syntax tree baselines (.fs.bsl) used by service tests
  - tests/fsharp/**: baseline/expected output in the legacy FSharpSuite tests

Net10 BCL copy/adjustment spec (proposed)

Goal
- Provide a repeatable plan for copying netstandard baselines/BCL-related outputs into net10, adjusting the framework labels from netstandard* to runtime/net10 where required.

Deliverables
1) A “net10 BCL baseline” folder with copied baselines.
2) A plan describing which baseline families are copied and how their names/content are rewritten.
3) A script to copy/rename BSLs (optional, provided below).

Constraints
- Preserve existing baselines; net10 copies go into a new folder (do not overwrite).
- Keep naming consistent with current baseline conventions and adjust references accordingly if a new folder is used.

Spec plan
1) Identify netstandard baselines to migrate
   - ILVerify baselines for FSharp.Core + FSharp.Compiler.Service currently on netstandard2.0/2.1.
   - SurfaceArea baselines for netstandard20/netstandard21.
   - Any other BCL baselines with “netstandard” in the name that are tied to core libs and not to compiler IL tests.

2) Define net10 baseline folder
   - Proposed folder: tests/ILVerify/net10/ or tests/BCL/net10/ (choose final location).
   - Recommended: keep per-suite folders but add net10 subfolder to avoid changing lookup logic.

3) Copy rules
   - ILVerify baselines:
     - Copy each “*_netstandard2.0.bsl” / “*_netstandard2.1.bsl” to “*_net10.0.bsl”.
   - SurfaceArea baselines:
     - Copy “FSharp.Core.SurfaceArea.netstandard20.*.bsl” + “netstandard21.*.bsl” -> “FSharp.Core.SurfaceArea.net10.0.*.bsl” (or “net10” if naming convention is simplified).
     - Copy “FSharp.Compiler.Service.SurfaceArea.netstandard21.bsl” -> “FSharp.Compiler.Service.SurfaceArea.net10.0.bsl”.

4) Adjust content from [netstandard] to [runtime]
   - If baseline text contains framework tags in brackets (e.g., [netstandard2.1]) replace with [runtime] or [net10.0] depending on agreed convention.
   - Keep the original netstandard baselines for backward compatibility.

5) Update lookup logic (if needed)
   - If tests select baselines via framework strings (SurfaceArea), add a “net10.0” case to the platform selection or parameterize it with a target framework constant.
   - For ILVerify baselines, ensure the test runner uses “net10.0” for net10 runs (if not already done).

6) Verify
   - Run baseline update if needed (TEST_UPDATE_BSL=1) and verify diffs.
   - Ensure CI still uses Release baselines for ILVerify as noted in DEVGUIDE.md.

Optional copy script (example)

You can use this script to copy/rename baselines. It creates net10 copies in-place, preserving originals:

```bash
#!/usr/bin/env bash
set -euo pipefail

# ILVerify baselines: netstandard -> net10.0
find tests/ILVerify -name "*netstandard*.bsl" -print0 | while IFS= read -r -d '' f; do
  net10_name=$(echo "$f" | sed -E 's/netstandard2\.[01]/net10.0/g')
  cp "$f" "$net10_name"
  # optional content adjust: replace [netstandard*] markers with [runtime]
  sed -i -E 's/\[netstandard[0-9\.]+\]/[runtime]/g' "$net10_name"
  echo "Copied $f -> $net10_name"
 done

# SurfaceArea baselines
for f in tests/FSharp.Core.UnitTests/FSharp.Core.SurfaceArea.netstandard*.bsl \
         tests/FSharp.Compiler.Service.Tests/FSharp.Compiler.Service.SurfaceArea.netstandard*.bsl; do
  [ -e "$f" ] || continue
  net10_name=$(echo "$f" | sed -E 's/netstandard[0-9]+/net10.0/g')
  cp "$f" "$net10_name"
  sed -i -E 's/\[netstandard[0-9\.]+\]/[runtime]/g' "$net10_name"
  echo "Copied $f -> $net10_name"
 done
```

If you want me to wire this into the build/test scripts or pick a concrete net10 baseline folder, let me know and I’ll extend the spec and implement it.

Next steps
- Confirm target location/naming for net10 baselines (in-place vs. new folder).
- Confirm whether bracketed markers should become [runtime] or [net10.0].
- Decide if I should implement and commit the script into the repo (e.g., under tests/scripts/).