namespace FSharp.Compiler.Interop

open Microsoft.FSharp.Core

// [<AttributeUsage (AttributeTargets.Class ||| AttributeTargets.Struct ||| AttributeTargets.Assembly, AllowMultiple=true)>]  
// [<Sealed>]
// type AutoOpenAttribute(path:string) =
//     inherit Attribute()
//     member _.Path = path
//     new() =  AutoOpenAttribute("")

// [<AutoOpen>]
[<Microsoft.FSharp.Core.AutoOpen>]
module Helpers =
    // let inline isNull (value : 'T when 'T : null) = 
    //     match box value with 
    //     | null -> true 
    //     | _ -> false

    let inline internal isNull (value: 'T when 'T: null) = 
        match value with 

        | null -> true 
        | _ -> false    