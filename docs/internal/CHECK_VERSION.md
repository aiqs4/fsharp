In der interaktiven Shell (dotnet fsi) funktionieren Präprozessor-Direktiven wie #if NET10_0 leider nicht zuverlässig, da fsi eine eigene Umgebung ist und nicht alle Symbole des SDK-Projekts automatisch übernimmt.
Um deine Umgebung direkt in dotnet fsi zu prüfen, nutzt du am besten die .NET Runtime API:
## 1. .NET Version & Framework prüfen

open System
open System.Runtime.InteropServices

// Welche .NET Version läuft gerade? (z.B. .NET 9.0.0 oder .NET 10.0.0)
let runtimeVer = RuntimeInformation.FrameworkDescription

// Welches TargetFramework (TFM) wird simuliert?
let tfm = 
    let assembly = typeof<obj>.Assembly
    let attr = assembly.CustomAttributes |> Seq.find (fun a -> a.AttributeType.Name = "TargetFrameworkAttribute")
    attr.ConstructorArguments.[0].Value.ToString()

printfn "Runtime: %s" runtimeVer
printfn "TFM: %s" tfm

## 2. Produktname & fsc.dll (Compiler) finden
Da fsi den F#-Compiler nutzt, kannst du den Pfad zur aktuell geladenen F#-Library finden:

let fsharpCorePath = typeof<list<int>>.Assembly.Location
let fsharpCompilerPath = 
    let fscAssembly = System.Reflection.Assembly.GetEntryAssembly()
    if fscAssembly <> null then fscAssembly.Location else "Unbekannt"

printfn "FSharp.Core Pfad: %s" fsharpCorePath
printfn "Einstiegspunkt (Compiler/FSI): %s" fsharpCompilerPath

## 3. Bin ich "netstandard"?
In der interaktiven Shell bist du technisch gesehen fast nie in netstandard, sondern immer in einer konkreten Implementierung (CoreCLR). Du kannst aber prüfen, ob die geladene FSharp.Core für netstandard gebaut wurde:

let isNetStandard = 
    typeof<list<int>>.Assembly.GetCustomAttributes(true)
    |> Seq.exists (fun a -> a.ToString().Contains("NetStandard"))

printfn "Ist FSharp.Core für NetStandard? %b" isNetStandard

## 4. Welches "fsi" nutze ich gerade?
Wenn du wissen willst, ob du gerade dein selbst gebautes dotnet fsi aus dem Repository nutzt oder das global installierte:

Environment.GetCommandLineArgs().[0] |> printfn "FSI Pfad: %s"

Warum hilft das bei deinem List.Create Fehler?
Wenn du in dotnet fsi deine gepatchte FSharp.Core.dll testen willst, musst du sie explizit laden, sonst nutzt FSI die Standard-Bibliothek des Systems:
#r "/pfad/zu/deiner/fsharp-wasm/artifacts/bin/FSharp.Core/Debug/net10.0/FSharp.Core.dll"
Soll ich dir zeigen, wie du dein lokal gebautes FSharp.Core direkt in FSI referenzierst, um zu sehen, ob List.Create dort auftaucht?


