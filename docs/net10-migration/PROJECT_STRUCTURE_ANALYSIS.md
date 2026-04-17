# F# Compiler net10.0 Project Structure Analysis

## ESSENTIAL PROJECTS (Required for Core Functionality)

### 1. **FSharp.Core** (`src/FSharp.Core/`)
- **Purpose:** F# standard library and runtime support
- **Needed for:** Everything - the foundation
- **Target:** netstandard2.0, netstandard2.1
- **Status:** ❓ NEEDS ANALYSIS

### 2. **Compiler** (`src/Compiler/FSharp.Compiler.Service.fsproj`)
- **Purpose:** Core F# compiler implementation
- **Needed for:** `dotnet fsc`, `dotnet fsi`, language services
- **Target:** netstandard2.0, netstandard2.1 (+ net10.0 conditional)
- **Status:** ❓ NEEDS ANALYSIS & WRAPPING
- **Dependencies:** FSharp.Core
- **Nullable usages expected:** System.Reflection, System.Collections, System.IO patterns

### 3. **fsc Driver** (`src/fsc/fscProject/fsc.fsproj`)
- **Purpose:** Command-line F# compiler entry point
- **Needed for:** `dotnet fsc` command
- **Target:** net10.0 (dynamic)
- **Status:** ❌ NEEDS WRAPPING
- **Dependencies:** Compiler, FSharp.Build, FSharp.Core, FSharp.DependencyManager.Nuget

### 4. **fsi Driver** (`src/fsi/fsiProject/fsi.fsproj`)
- **Purpose:** Interactive F# REPL entry point
- **Needed for:** `dotnet fsi` command
- **Target:** net10.0 (dynamic)
- **Status:** ❌ NEEDS WRAPPING
- **Dependencies:** Compiler, FSharp.Compiler.Interactive.Settings, FSharp.Core

### 5. **FSharp.Compiler.Interactive.Settings** (`src/FSharp.Compiler.Interactive.Settings/`)
- **Purpose:** Configuration for FSI (REPL)
- **Needed for:** `dotnet fsi` only
- **Target:** net10.0
- **Status:** ❌ NEEDS WRAPPING
- **Dependencies:** FSharp.Core

### 6. **FSharp.Build** (`src/FSharp.Build/`)
- **Purpose:** MSBuild integration for F# project compilation
- **Needed for:** Building F# projects via MSBuild
- **Target:** net10.0
- **Status:** ✅ COMPLETE (Phase 1)
- **Dependencies:** FSharp.Core, FSharp.Compiler.Service

### 7. **FSharp.DependencyManager.Nuget** (`src/FSharp.DependencyManager.Nuget/`)
- **Purpose:** NuGet package resolution for FSI/compiler
- **Needed for:** Loading dependencies in FSI and compiler context
- **Target:** netstandard2.0, net10.0
- **Status:** ❌ NEEDS ANALYSIS
- **Dependencies:** FSharp.Core

---

## OPTIONAL/LEGACY PROJECTS (CAN BE REMOVED)

### 1. **Microsoft.FSharp.Compiler** (`src/Microsoft.FSharp.Compiler/`)
- **Purpose:** Wrapper project (possibly legacy)
- **Status:** ⚠️ Likely redundant with fsc driver
- **Recommendation:** REMOVE - check if fsc driver is primary

### 2. **FSharp.Compiler.LanguageServer** (`src/FSharp.Compiler.LanguageServer/`)
- **Purpose:** LSP server for editor integration (VSCode, Vim, etc.)
- **Needed for:** Editor support only
- **Target:** net8.0
- **Status:** 🔧 Optional but nice to have
- **Recommendation:** Fix separately if needed

### 3. **LegacyMSBuildResolver** (`src/LegacyMSBuildResolver/`)
- **Purpose:** Legacy reference resolver (used by fsc/fsi drivers)
- **Needed for:** Young projects using legacy MSBuild
- **Status:** ❓ Check if still needed
- **Recommendation:** Keep for now, needed by fsc/fsi

### 4. **Microsoft.CommonLanguageServerProtocol.Framework.Proxy** (`src/Microsoft.CommonLanguageServerProtocol.Framework.Proxy/`)
- **Purpose:** Supporting library for LSP server
- **Status:** Only needed if keeping FSharp.Compiler.LanguageServer
- **Recommendation:** REMOVE if removing LSP server

---

## PHASE 2: PROJECTS TO WRAP FOR net10.0

### Priority Order:
1. **FSharp.Compiler.Interactive.Settings** - Small, few dependencies
2. **Compiler** - Large, many System.* dependencies (System.Reflection, System.Collections.Generic)
3. **fsc/fsi drivers** - Entry points, should be straightforward after Compiler is done
4. **FSharp.DependencyManager.Nuget** - Medium complexity
5. **FSharp.Compiler.LanguageServer** - Optional, target net8.0 initially

---

## REQUIRED WRAPPER MODULES FOR PHASE 2

### For FSharp.Compiler.Interactive.Settings & Compiler:
- ✅ String (Phase 1)
- ✅ Path (Phase 1)
- ✅ Directory (Phase 1)
- ✅ File (Phase 1)
- ✅ Encoding (Phase 1)
- ✅ Environment (Phase 1)
- ❌ **System.Reflection** - Type/Assembly loading (HIGH PRIORITY)
- ❌ **System.Collections.Generic** - Dictionary/List operations
- ❌ **System.Threading** - Thread/lock operations
- ❌ **System.Diagnostics** - Process operations

---

## BUILD STRATEGY

```
Phase 1: ✅ COMPLETE
├─ FSharp.Compiler.Interop (IO, Text, Environment)
└─ FSharp.Build (using Phase 1 wrappers)

Phase 2a: IN PROGRESS
├─ Extend FSharp.Compiler.Interop (Reflection, Collections, Threading)
├─ FSharp.Compiler.Interactive.Settings
└─ Compiler (FSharp.Compiler.Service)

Phase 2b:
├─ fsc driver
├─ fsi driver
├─ fsc/fsi/fsiProject/fsc.fsproj updates
└─ LegacyMSBuildResolver (if needed)

Phase 3: Optional
└─ FSharp.Compiler.LanguageServer (target net8.0 or leave as-is)
```

---

## PROJECTS TO SKIP/REMOVE

| Project | Reason | Action |
|---------|--------|--------|
| `Microsoft.FSharp.Compiler` | Possibly legacy wrapper | Investigate & likely REMOVE |
| `Microsoft.CommonLanguageServerProtocol.Framework.Proxy` | LSP support library only | Remove unless keeping LSP server |
| Keep: `LegacyMSBuildResolver` | Used by fsc/fsi drivers | DO NOT REMOVE |

---

## TEST MATRIX

After all phases complete:

| Tool | Framework | Status |
|------|-----------|--------|
| `dotnet fsc` | net10.0 | Must work |
| `dotnet fsi` | net10.0 | Must work |
| `FSharp.Compiler.Service` | net10.0 | Must work |
| FSharp projects built with MSBuild | Via FSharp.Build | Must work |
| Language Server | net8.0 | Optional |
| NuGet package manager in FSI | net10.0 | Must work |

