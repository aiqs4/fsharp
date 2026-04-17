# .NET Interoperability Wrapper Specification

## Overview

This document specifies the .NET/C# interoperability layer needed to make the F# compiler, FSI interactive, and language server compatible with `net10.0` which enforces strict null checking.

The goal is to create a dedicated `FSharp.Compiler.IO` library that wraps all .NET BCL (Base Class Library) calls, eliminating direct dependencies on potentially null-returning functions and providing nullability-aware F# bindings.

## Problem Statement

- .NET 10.0 enforces strict null checking
- Current code uses direct C# interop that returns nullable types
- Examples: `Path.GetDirectoryName()`, `Directory.GetFiles()`, `Assembly.Location`, etc.
- These functions can return `null`, causing compiler errors with `#nullable enable`

## Solution Architecture

### 1. Create Wrapper Library

**Project Name:** `FSharp.Compiler.Interop`
**Location:** `src/Compiler/FSharp.Compiler.Interop/FSharp.Compiler.Interop.fsproj`
**Output:** Library targeting `net10.0;netstandard2.1`

### 2. System Namespaces to Wrap

#### System.IO
- **Path functions:**
  - `Path.Combine(paths: string[]): string`
  - `Path.GetDirectoryName(path: string): string | None`
  - `Path.GetFileName(path: string): string | None`
  - `Path.GetFileNameWithoutExtension(path: string): string | None`
  - `Path.GetExtension(path: string): string | None`
  - `Path.GetFullPath(path: string): string`
  - `Path.GetTempPath(): string`
  - `Path.GetTempFileName(): string`
  - `Path.DirectorySeparatorChar: char`

- **Directory functions:**
  - `Directory.Exists(path: string): bool`
  - `Directory.CreateDirectory(path: string): DirectoryInfo`
  - `Directory.GetFiles(path: string, pattern?: string): string[]`
  - `Directory.GetDirectories(path: string, pattern?: string): string[]`
  - `Directory.GetCurrentDirectory(): string`
  - `Directory.SetCurrentDirectory(path: string): unit`
  - `Directory.Delete(path: string, recursive?: bool): unit`

- **File functions:**
  - `File.Exists(path: string): bool`
  - `File.ReadAllText(path: string, encoding?: Encoding): string`
  - `File.ReadAllBytes(path: string): byte[]`
  - `File.WriteAllText(path: string, contents: string, encoding?: Encoding): unit`
  - `File.WriteAllBytes(path: string, bytes: byte[]): unit`
  - `File.AppendAllText(path: string, contents: string, encoding?: Encoding): unit`
  - `File.Delete(path: string): unit`
  - `File.Copy(sourceFileName: string, destFileName: string, overwrite?: bool): unit`
  - `File.Move(sourceFileName: string, destFileName: string, overwrite?: bool): unit`
  - `File.OpenRead(path: string): FileStream`
  - `File.OpenWrite(path: string): FileStream`

#### System.Text
- **Encoding:**
  - `Encoding.UTF8: Encoding`
  - `Encoding.Unicode: Encoding`
  - `Encoding.ASCII: Encoding`
  - `Encoding.Default: Encoding`
  - `encoding.GetString(bytes: byte[], index: int, count: int): string`
  - `encoding.GetBytes(s: string): byte[]`

- **StringBuilder:**
  - `StringBuilder.Clear(): StringBuilder`
  - `StringBuilder.Append(value: string): StringBuilder`
  - `StringBuilder.AppendLine(value?: string): StringBuilder`
  - `StringBuilder.ToString(): string`

#### System.Collections.Generic
- `Dictionary<'K, 'V>: new() : unit`
- `Dictionary<'K, 'V>.Add(key: 'K, value: 'V): unit`
- `Dictionary<'K, 'V>.Remove(key: 'K): bool`
- `Dictionary<'K, 'V>.TryGetValue(key: 'K, value: outref<'V>): bool`
- `Dictionary<'K, 'V>.ContainsKey(key: 'K): bool`
- `List<'T>: new() : unit`
- `List<'T>.Add(item: 'T): unit`
- `List<'T>.RemoveAt(index: int): unit`
- `List<'T>.Count: int`
- `List<'T>.ToArray(): 'T[]`
- `HashSet<'T>: new() : unit`
- `HashSet<'T>.Add(item: 'T): bool`
- `HashSet<'T>.Remove(item: 'T): bool`
- `HashSet<'T>.Contains(item: 'T): bool`

#### System.Reflection
- `Assembly.Location: string | None`
- `Assembly.GetName(): AssemblyName`
- `Assembly.GetTypes(): Type[]`
- `Assembly.GetType(name: string, throwOnError?: bool): Type | None`
- `Assembly.GetExecutingAssembly(): Assembly`
- `Assembly.GetEntryAssembly(): Assembly | None`
- `Assembly.LoadFrom(assemblyFile: string): Assembly`
- `Type.Name: string`
- `Type.FullName: string | None`
- `Type.AssemblyQualifiedName: string | None`
- `Type.Namespace: string | None`
- `Type.Assembly: Assembly`
- `Type.GetCustomAttributes(inherit?: bool): obj[]`

#### System.Diagnostics
- `Debug.Assert(condition: bool, message?: string): unit`
- `Debug.WriteLine(message: obj): unit`
- `Stopwatch: new() : unit`
- `Stopwatch.Start(): unit`
- `Stopwatch.Stop(): unit`
- `Stopwatch.ElapsedMilliseconds: int64`
- `Process.Start(startInfo: ProcessStartInfo): Process | None`
- `Process.Exit(exitCode: int): unit`

#### System.Threading / System.Threading.Tasks
- `Task.Run(func: unit -> 'T): Task<'T>`
- `Task.WaitAll(tasks: Task[]): unit`
- `Environment.ProcessorCount: int`

#### System.Text.RegularExpressions
- `Regex: new(pattern: string, options?: RegexOptions) : unit`
- `Regex.Match(input: string): Match`
- `Regex.Matches(input: string): MatchCollection`
- `Regex.Replace(input: string, replacement: string): string`

#### System (Core types)
- **String operations:**
  - `String.IsNullOrEmpty(value: string | None): bool`
  - `String.IsNullOrWhiteSpace(value: string | None): bool`
  - `String.Concat(values: string[]): string`
  - `String.Join(separator: string, values: string[]): string`
  - `String.Format(format: string, args: obj[]): string`
  - `String.Compare(strA: string, strB: string, comparison?: StringComparison): int`
  - `String.Equals(a: string, b: string, comparison?: StringComparison): bool`
  - `String.Empty: string`
  - `string.Length: int`
  - `string.Substring(startIndex: int, length?: int): string`
  - `string.Split(separator: char[], options?: StringSplitOptions): string[]`
  - `string.Trim(trimChars?: char[]): string`
  - `string.ToLower(): string`
  - `string.ToUpper(): string`

- **Environment:**
  - `Environment.GetEnvironmentVariable(variable: string): string | None`
  - `Environment.SetEnvironmentVariable(variable: string, value: string | None): unit`
  - `Environment.NewLine: string`
  - `Environment.Exit(exitCode: int): unit`

#### System.Globalization
- `CultureInfo.InvariantCulture: CultureInfo`
- `CultureInfo.CurrentCulture: CultureInfo`
- `CultureInfo.CurrentUICulture: CultureInfo`

#### System.IO.Compression
- `ZipFile.ExtractToDirectory(sourceArchiveFileName: string, destinationDirectoryName: string): unit`
- `ZipFile.CreateFromDirectory(sourceDirectoryName: string, destinationArchiveFileName: string): unit`

## Wrapper Design Pattern

### Option-based Nullability

All functions that can return `null` will return `'T option`:

```fsharp
module Path =
  let getDirectoryName (path: string): string option =
    let result = System.IO.Path.GetDirectoryName path
    if result = null then None else Some result

  let getFileName (path: string): string option =
    let result = System.IO.Path.GetFileName path
    if result = null then None else Some result

  let combine (paths: string[]): string =
    System.IO.Path.Combine paths
```

### Guaranteed Non-Null

Functions that never return null will be standard:

```fsharp
module Path =
  let directorySeperatorChar: char = System.IO.Path.DirectorySeparatorChar

  let combine (paths: string[]): string =
    System.IO.Path.Combine paths
```

### Resource Management

Proper disposal of streams, readers, writers:

```fsharp
module File =
  let readAllBytes (path: string): byte[] =
    System.IO.File.ReadAllBytes path

  let readAllText (path: string): string =
    System.IO.File.ReadAllText path
```

## Implementation Phases

### Phase 1: Create Core Wrapper Library
- Create `FSharp.Compiler.Interop.fsproj`
- Implement wrappers for System.IO.Path, System.IO.Directory, System.IO.File
- Implement wrappers for System.Text.Encoding
- Implement wrappers for System primitives (String, Environment)

### Phase 2: Implement Collections & Reflection Wrappers
- Dictionary, List, HashSet wrappers
- Assembly, Type reflection wrappers
- CultureInfo wrappers

### Phase 3: Implement Diagnostics & Threading Wrappers
- Debug, Stopwatch wrappers
- Process wrappers
- Task/Threading wrappers

### Phase 4: Global Replacement
- Replace all `open System.IO` with opens of wrapper modules
- Replace all direct `Path.GetDirectoryName` calls with wrapper calls
- Search and replace patterns across all source files:
  - `System.IO.Path.` → `FSharp.Compiler.Interop.Path.`
  - `System.Text.Encoding.` → `FSharp.Compiler.Interop.Encoding.`
  - etc.

### Phase 5: Update Project References
- Update all `.fsproj` files to reference `FSharp.Compiler.Interop`
- Update all `Directory.Build.props` as needed

## Critical Files to Update

### Compiler Projects
- `src/Compiler/FSharp.Compiler.Service.fsproj`
- `src/fsc/fscProject/fsc.fsproj`
- `src/fsi/fsiProject/fsi.fsproj`
- `src/FSharp.Build/FSharp.Build.fsproj`
- `src/FSharp.Compiler.LanguageServer/FSharp.Compiler.LanguageServer.fsproj`

### Build System
- `Directory.Build.props` - Add reference to new project
- `Directory.Build.targets` - Update if necessary

## Testing Strategy

1. **Unit Tests:** Create tests for each wrapper function
2. **Integration Tests:** Verify compiler builds with `net10.0`
3. **Compatibility Tests:** Ensure `netstandard2.1` builds still work
4. **Functional Tests:** Run existing compiler tests with wrapped code

## Success Criteria

- ✅ FSharp.Build compiles for net10.0
- ✅ Compiler compiles for net10.0
- ✅ FSI compiles for net10.0
- ✅ Language Server compiles for net10.0
- ✅ All existing tests pass
- ✅ No nullable type warnings when `#nullable enable` is used
- ✅ Backward compatibility maintained for other frameworks

## Next Steps

1. Approve this specification
2. Create the wrapper library project structure
3. Implement Phase 1 (Core wrappers)
4. Begin systematic replacement in source files
