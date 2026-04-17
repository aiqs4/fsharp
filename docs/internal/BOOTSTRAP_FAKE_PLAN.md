**Anweisungen für den AI Coder: Implementierung des FAKE Build Systems für das F# Compiler Repository**

Du bist Experte für F# und FAKE-Build-Pipelines. Deine Aufgabe ist es, das aktuelle bash+MSBuild-Bootstrap (eng/build.sh + proto.proj + alle .props/.targets-Dateien) durch ein **reines F#-FAKE-Build-System** zu ersetzen, wie in **BOOTSTRAP_FAKE_SPEC.md** spezifiziert.

Ziel ist ein **typsicheres, wartbares, cross-platform-fähiges** Build, das exakt dieselben Artefakte erzeugt wie `./build.sh -c Release` (inkl. Bootstrap-Phase mit `proto.proj` und anschließender Self-Compilation mit Redirect via Bootstrap-Compiler).

**Wichtige Quellen (alle bereits im Repo vorhanden):**
- **BOOTSTRAP_FAKE_SPEC.md** → Hoch-Level-Design und Modul-Skizzen (dein Haupt-Blaupause).
- **BOOTSTRAP_GUIDE.md** + **BOOTSTRAP_QUICK_REFERENCE.md** + **BOOTSTRAP_FILES_REFERENCE.md** → Exakte Abläufe, Properties, Conditions und Dateien.
- **BOOTSTRAP_INSIGHTS.md** → Net10.0-spezifische Fallstricke (OutputType=Exe im Proto, RuntimeIdentifier-Conditionals, runtimeconfig.json, etc.).
- Die Liste aller `.fsproj|.targets|.props`-Dateien (am Ende der Query) → Du musst sie **nicht** alle umschreiben, aber verstehen, welche Properties im FAKE-Build explizit gesetzt werden müssen.

### Phase 1 – Ziel (was du jetzt baust)
- Paralleles FAKE-Build (`build/`-Ordner) neben dem alten bash-Build.
- `fake build` muss **bit-für-bit identische** Artefakte erzeugen wie `./build.sh -c Release`.
- Spätere Phasen (2+3) entfernen dann bash und die MSBuild-Conditionals – dafür ist das FAKE-System schon vorbereitet.

### Voraussetzungen / Setup
1. Erstelle den Ordner `build/` im Repository-Root.
2. Verwende **FAKE 6** (aktuell empfohlen).
3. Erstelle folgende Dateien (genaue Struktur aus dem Spec):

```
build/
├── Build.fs                  ← Entry Point
├── Targets.fs                ← Öffentliche Targets + Dependencies
├── Bootstrap.fs              ← Proto-Phase
├── Compiler.fs               ← Self-Compilation-Phase
├── Config.fs                 ← Typ-sichere Konfiguration + Argument-Parsing
├── Paths.fs                  ← Alle Pfade (Single Source of Truth)
├── Artifacts.fs              ← Validierung + Artefakt-Handling
├── build.fsx                 ← FAKE-Einstiegspunkt (sehr kurz)
├── paket.dependencies        ← FAKE-Dependencies
└── paket.lock                ← (wird von paket generiert)
```

4. `paket.dependencies` Inhalt:
```
framework: net8.0
source https://api.nuget.org/v3/index.json

nuget Fake.Core.Target
nuget Fake.DotNet.Cli
nuget Fake.Core.Environment
nuget Fake.Core.Process
nuget Fake.Core.Trace
nuget Fake.IO.FileSystem
```

5. `build.fsx` (minimal):
```fsharp
#r "paket:
    source https://api.nuget.org/v3/index.json
    nuget Fake.Core.Target
    nuget Fake.DotNet.Cli // ... (restliche aus paket.dependencies)
    //"
#load "Build.fs"
open Fake.Core
open Fake.Core.TargetOperators

// Nur den Main-Einstieg aufrufen
Build.Main.main (System.Environment.GetCommandLineArgs())
```

6. Thin Wrapper `build.sh` (ersetzt später den alten):
```bash
#!/bin/bash
set -e
cd "$(dirname "$0")"
if command -v fake &> /dev/null; then
  fake build "$@"
else
  echo "FAKE not found. Install: dotnet tool install -g fake-cli"
  exit 1
fi
```

### Modul-für-Modul-Implementierung (genaue Reihenfolge)

**1. Paths.fs**  
Kopiere die Skizze aus dem Spec und erweitere sie mit **allen** wichtigen Pfaden aus BOOTSTRAP_FILES_REFERENCE.md und BOOTSTRAP_INSIGHTS.md:
- `artifacts/Bootstrap/{fslex,fsyacc,fsc,fsi,AssemblyCheck}`
- `bootstrapFscRuntimeConfig` (wichtig für net10.0!)
- `ProtoOutputPath`, `FSharpNetCoreProductTargetFramework` etc.
- `ensureExists` + `validateBootstrap` (muss `fsc.runtimeconfig.json` und `Microsoft.FSharp.Targets` prüfen).
- Hilfsfunktionen: `releaseFscDll`, `releaseCoreDll`, `bootstrapFscExe` etc.

**2. Config.fs**  
Erweitere die Skizze zu einem **vollständigen** Parser:
- Unterstütze alle Flags aus `eng/build.sh`: `--configuration`, `--tfm`, `--bootstrap` (force), `--verbose`, `--skip-tests`, `--binarylog`, etc.
- Verwende `Fake.Core.Context` + einfaches `argv`-Parsing oder `Argu` (optional, aber sauber).
- `TargetFramework` als DU mit `AsString` + `AsMSBuildProperty`.
- `BuildPhase` + `BuildOptions` exakt wie im Spec.
- `parseArgs` muss `Result<BuildOptions, string>` zurückgeben und sinnvolle Defaults setzen.

**3. Bootstrap.fs**  
- `clean()` → `Shell.rm_rf Paths.artifactsBootstrap`
- `build opts` → `DotNet.publish` mit exakt diesen Argumenten (aus BOOTSTRAP_GUIDE.md):
  ```fsharp
  let publishArgs = [
      "publish"
      Paths.protoProj
      "-c"; "Proto"
      "/p:DotNetBuild=false"
      "/p:DotNetBuildSourceOnly=false"
      if opts.Verbose then "-v"; "diag"
  ]
  DotNet.publish (fun p -> { p with WorkingDirectory = Paths.root }) publishArgs
  ```
- `validate()` → `Paths.validateBootstrap()` + zusätzliche Checks aus Insights (runtimeconfig.json, FSharp.Build.dll, Microsoft.FSharp.Targets).
- `run opts` → Logik mit `BootstrapOnly` / `CleanRebuild` / `FullRelease` exakt wie im Spec.

**4. Compiler.fs** (die kritische Stelle)
- `build opts`:
  - Zuerst `Bootstrap.validate()`
  - Dann `DotNet.build` (oder besser `MSBuild.run` für maximale Kontrolle) auf `FSharp.sln`.
  - Wichtige `/p:`-Parameter (aus eng/build.sh + BOOTSTRAP_FILES_REFERENCE.md):
    - `-c` Release/Debug
    - `/p:FSharpNetCoreProductTargetFramework=...` (aus opts)
    - `/p:DisableCompilerRedirection=` (leer lassen → Redirect aktiviert, weil Bootstrap existiert)
    - Optional: `/p:FSHARP_TARGETS_PATH=...` falls du die MSBuild-Conditionals später komplett entfernst.
- Für **Phase 1** (parallel): Verlasse dich auf die bestehenden Conditionals in `FSharpBuild.Directory.Build.props`. Das ist gewollt.
- Für spätere Phasen: Setze explizit alle Shims (`FSharpTargetsPath`, `FSharpPropsShim`, `FSharpTargetsShim`, `FSharpBuildAssemblyFileOverride`).

**5. Artifacts.fs**  
- Funktionen für `collectArtifacts`, `validateReleaseArtifacts`, `cleanBin`, `packNuGets` (später).
- Nutze `Paths.ensureExists` für alle wichtigen Dateien.

**6. Targets.fs**  
- Genau wie im Spec + zusätzliche Targets:
  - `Clean`
  - `BuildBootstrap`
  - `BuildCompiler`
  - `Verify`
  - `Build` (default)
  - `Rebuild`
  - `Test` (optional, später)
  - Dependencies mit `Target.dependsOn` exakt wie skizziert.

**7. Build.fs**  
- Entry Point exakt wie im Spec.
- Rufe `Targets.initTargets opts` auf.
- Gutes Error-Handling mit `Trace.error` und Exit-Code.

### Wichtige Details & Fallstricke (aus BOOTSTRAP_INSIGHTS.md)

- Im Proto-Build muss `fsc.fsproj` `OutputType=Exe` bekommen → wird durch die Condition in der .fsproj automatisch erledigt, solange du `/p:Configuration=Proto` setzt.
- `runtimeconfig.json` **muss** im Bootstrap vorhanden sein.
- `RuntimeIdentifier`, `PublishReadyToRun`, `UseAppHost` dürfen **nur** außerhalb von Proto gesetzt werden.
- Alle `Condition="'$(Configuration)' != 'Proto'"` in den .props bleiben für Phase 1 erhalten.

### Test- & Validierungs-Workflow (nach jedem Modul)

1. `dotnet tool restore` (falls nötig) → `fake build`
2. Vergleiche mit altem Build:
   ```bash
   ./build.sh -c Release          # altes Build
   fake build                     # neues Build
   ```
3. Prüfe mit der **Validation Checklist** aus BOOTSTRAP_FAKE_SPEC.md.
4. Teste explizit:
   - `fake build --target BuildBootstrap`
   - `fake build --target Rebuild`
   - `fake build -- --tfm net9.0`
   - `fake build -- --bootstrap`
   - `fake build -- --verbose`

### Nächste Schritte nach Phase 1 (nicht jetzt, aber vorbereiten)

- Phase 2: `build.sh` ruft intern `fake build` auf (beide Wege funktionieren).
- Phase 3: Entferne bash-Logik aus `eng/build.sh` und die meisten Conditionals aus den .props/.targets → FAKE setzt alle Properties explizit.

**Fertig?**  
Sobald `fake build` identische Artefakte wie `./build.sh -c Release` erzeugt und alle Checks aus der Validation Checklist grün sind, bist du fertig.

Du kannst loslegen. Wenn du Fragen zu einem Modul hast, frag nach – ich helfe bei jedem Schritt. Viel Erfolg! Das wird ein riesiger Gewinn für das Repository.