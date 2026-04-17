You are correct that the F# repository can be confusing due to its evolution. Microsoft.FSharp.Compiler is generally redundant for modern dotnet workflows and is likely a legacy artifact from the transition between the old Visual F# (desktop) and modern .NET.
## Is Microsoft.FSharp.Compiler Needed?
No. For a modern net10.0 or net8.0 SDK:

* dotnet fsc relies on the fsc driver (src/fsc/fsc.fsproj) which consumes FSharp.Compiler.Service.dll.
* dotnet build relies on FSharp.Build (src/FSharp.Build/), which acts as the MSBuild task and invokes the compiler logic via the same service or driver.
* Microsoft.FSharp.Compiler was often a wrapper used for older Visual Studio deployments or specific GAC-related installs. In the dotnet/fsharp repo, the FSharp.Compiler.Service project is the true "engine" of everything. [1, 2] 

## Core Dependency Map
To ensure dotnet fsc, dotnet fsi, and dotnet build work, you only need to focus on these:

| Tool | Primary Entry Project | Critical Dependencies |
|---|---|---|
| dotnet fsc | src/fsc/fsc.fsproj | FSharp.Compiler.Service, FSharp.Core, LegacyMSBuildResolver |
| dotnet fsi | src/fsi/fsi.fsproj | FSharp.Compiler.Service, FSharp.Compiler.Interactive.Settings, FSharp.DependencyManager.Nuget |
| dotnet build | src/FSharp.Build/ | FSharp.Compiler.Service, FSharp.Core |

## Corrections to Your Notes

   1. Compiler Project Name: You listed FSharp.Compiler.Service.fsproj as the core implementation. While it is the "Service" entry point, the actual compiler logic often lives in a shared project or is compiled into this service. In modern F# development, FCS (FSharp.Compiler.Service) is the standard for both the command line and the IDE.
   2. LegacyMSBuildResolver: Do not remove this yet. It is still heavily used by fsc and fsi to resolve assembly references when you aren't using a full MSBuild context (like in scripts or direct compiler calls).
   3. FSI Settings: FSharp.Compiler.Interactive.Settings is small but vital for fsi specifically. Removing it will break the REPL's ability to manage its own state (like #time or #quit). [3, 4, 5] 

## Recommendations for Your Strategy

* Discard Microsoft.FSharp.Compiler: If your goal is a lean net10.0 compiler, this wrapper is dead weight.
* LSP is Truly Optional: Most modern editors (Ionide, VS) use the FSharp.Compiler.Service directly or via their own LSP implementations. The repo's internal FSharp.Compiler.LanguageServer is not strictly required for the compiler to function.
* Targeting net10.0: Your priority on Reflection and Collections wrappers is correct. The compiler relies heavily on custom assembly loading and high-performance collections (like HashMultiMap).

Should I help you map out the specific System.Reflection types that the compiler uses most heavily to prioritize your Phase 2?

[1] [https://fsharp.github.io](https://fsharp.github.io/fsharp-compiler-docs/fcs/)
[2] [https://github.com](https://github.com/fsbolero/FSharp.Compiler.Service)
[3] [https://fsharp.github.io](https://fsharp.github.io/fsharp-compiler-docs/tooling-features.html)
[4] [https://fsharp.github.io](https://fsharp.github.io/fsharp-compiler-docs/fcs/)
[5] [https://github.com](https://github.com/fsharp/fslang-suggestions/issues/542)
---

To upgrade FSharp.Compiler.Service (FCS) for net10.0, your Phase 2 wrappers must address how the compiler interacts with binary metadata and the runtime. The compiler is "reflection-heavy" because it must read and sometimes execute code from referenced assemblies. [1, 2] 
## Core Reflection Usage in FCS
The following categories represent the "High Priority" System.Reflection calls you will encounter in the core compiler logic:
## 1. Assembly Resolution & Loading
The compiler needs to resolve and load binary references to understand the types available to your F# code. [3] 

* Assembly.Load / LoadFrom / LoadFile: Used primarily by fsi (Interactive) and the assembly resolver to bring DLLs into memory.
* AssemblyName.GetAssemblyName: Essential for the LegacyMSBuildResolver and NuGet dependency manager to verify versions before loading. [4] 

## 2. Type and Member Inspection
This is the "brain" of the type-checker. It uses reflection to "see" inside C# or F# DLLs you have referenced. [1] 

* Type.GetType / Type.GetMethods / Type.GetProperties: Used to build the internal symbol table for every external reference.
* Type.GetCustomAttributes: Critical for identifying F#-specific metadata (like [<AbstractClass>] or [<Extension>]) stored in compiled assemblies. [2, 3] 

## 3. F# Interactive (FSI) Execution
Unlike the static compiler (fsc), fsi actually runs code, which requires dynamic invocation. [4] 

* Activator.CreateInstance: Used to instantiate types defined or loaded during an interactive session.
* MethodInfo.Invoke / PropertyInfo.GetValue: Used to execute the compiled snippets in the REPL. [3, 5] 

## 4. Low-Level Metadata Support
The compiler often bypasses standard reflection for performance, using System.Reflection.Metadata. [6, 7] 

* Module.GetTypes: Used to scan assemblies for all available public types during the "open" namespace resolution process.
* ParameterInfo / FieldInfo: Required to map method signatures correctly between IL (Intermediate Language) and F# types. [3] 

## Phase 2 Implementation Steps

   1. Read-Only Priority: Focus on Type Inspection first. The static compiler (fsc) mostly "reads" metadata. If you get Type and MemberInfo wrappers working, dotnet fsc will start to breathe.
   2. Execution Second: Focus on Dynamic Invocation (Invoke, CreateInstance) only when you are ready to tackle dotnet fsi.
   3. Metadata vs. Reflection: Note that modern FCS heavily utilizes System.Reflection.Metadata for performance. Ensure your wrappers support this package, as it's often the actual source of truth for the compiler rather than standard runtime reflection. [3, 5, 6, 8] 

✅ The System.Reflection namespace is the primary bridge between the compiler's logical analysis and the physical DLLs it processes.
Would you like to start with a specific wrapper for System.Type or Assembly loading logic?

[1] [https://www.youtube.com](https://www.youtube.com/watch?v=b1_Qxw71xNg)
[2] [https://learn.microsoft.com](https://learn.microsoft.com/en-us/dotnet/api/system.reflection?view=net-10.0#:~:text=Contains%20types%20that%20retrieve%20information%20about%20assemblies%2C,To%20dynamically%20create%20types%2C%20use%20the%20System.)
[3] [https://www.youtube.com](https://www.youtube.com/watch?v=MqJ_JjCV-9M)
[4] [https://stackoverflow.com](https://stackoverflow.com/questions/57925368/how-to-use-results-of-fsharp-compiler-services)
[5] [https://www.nuget.org](https://www.nuget.org/packages/FSharp.Compiler.Service)
[6] [https://www.nuget.org](https://www.nuget.org/packages/FSharp.Compiler.Service/43.10.100-preview.25358.103#:~:text=.NETStandard%202.0%20*%20FSharp.Core%20%28%3E=%2010.0.100%2Dpreview.25358.103%29%20*,System.Reflection.Metadata%20%28%3E=%209.0.0%29%20*%20System.Runtime.CompilerServices.Unsafe%20%28%3E=%206.1.0%29)
[7] [https://www.nuget.org](https://www.nuget.org/packages/FSharp.Compiler.Service/32.0.0#:~:text=*%20.NETFramework%204.6.1.%20FSharp.Core%20%28%3E=%204.6.2%29%20System.Collections.Immutable,4.3.0%29%20System.Runtime.Loader%20%28%3E=%204.0.0%29%20System.Security.Cryptography.Algorithms%20%28%3E=%204.3.0%29)
[8] [https://www.nuget.org](https://www.nuget.org/packages/FSharp.Compiler.Service/43.9.300#:~:text=.NETStandard%202.0%20*%20FSharp.Core%20%28=%209.0.300%29%20*,System.Reflection.Metadata%20%28%3E=%208.0.0%29%20*%20System.Runtime.CompilerServices.Unsafe%20%28%3E=%206.1.0%29)
