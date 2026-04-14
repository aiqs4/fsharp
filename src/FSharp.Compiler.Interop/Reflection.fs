// Copyright (c) Sascha Dewald. See https://github.com/fsharp-wasm/fsharp

namespace FSharp.Compiler.Interop

/// Nullability-safe wrappers for System.Reflection operations
module Reflection =
    open System
    open System.Reflection

    /// Gets the type of an object, handling null safely
    let getType (obj: obj): Type option =
        if isNull obj then None
        else
            try
                Some(obj.GetType())
            with _ -> None

    /// Gets a type by name from the current assembly
    let getTypeByName (typeName: string): Type option =
        if String.isNullOrEmpty typeName then
            None
        else
            try
                let ty = Type.GetType(typeName)
                if isNull ty then None else Some ty
            with _ -> None

    /// Gets all methods of a type
    let getMethods (ty: Type): MethodInfo[] =
        if isNull ty then [||]
        else
            try
                ty.GetMethods()
            with _ -> [||]

    /// Gets a specific method by name
    let getMethod (name: string) (ty: Type): MethodInfo option =
        if isNull ty || String.isNullOrEmpty name then
            None
        else
            try
                let method = ty.GetMethod(name)
                if isNull method then None else Some method
            with _ -> None

    /// Gets a method with specific parameter types
    let getMethodWithParams (name: string) (paramTypes: Type[]) (ty: Type): MethodInfo option =
        if isNull ty || String.isNullOrEmpty name then
            None
        else
            try
                let method = ty.GetMethod(name, paramTypes)
                if isNull method then None else Some method
            with _ -> None

    /// Gets all properties of a type
    let getProperties (ty: Type): PropertyInfo[] =
        if isNull ty then [||]
        else
            try
                ty.GetProperties()
            with _ -> [||]

    /// Gets a specific property by name
    let getProperty (name: string) (ty: Type): PropertyInfo option =
        if isNull ty || String.isNullOrEmpty name then
            None
        else
            try
                let prop = ty.GetProperty(name)
                if isNull prop then None else Some prop
            with _ -> None

    /// Gets all fields of a type
    let getFields (ty: Type): FieldInfo[] =
        if isNull ty then [||]
        else
            try
                ty.GetFields()
            with _ -> [||]

    /// Gets a specific field by name
    let getField (name: string) (ty: Type): FieldInfo option =
        if isNull ty || String.isNullOrEmpty name then
            None
        else
            try
                let field = ty.GetField(name)
                if isNull field then None else Some field
            with _ -> None

    /// Gets all constructors of a type
    let getConstructors (ty: Type): ConstructorInfo[] =
        if isNull ty then [||]
        else
            try
                ty.GetConstructors()
            with _ -> [||]

    /// Gets a constructor with specific parameter types
    let getConstructorWithParams (paramTypes: Type[]) (ty: Type): ConstructorInfo option =
        if isNull ty then
            None
        else
            try
                let ctor = ty.GetConstructor(paramTypes)
                if isNull ctor then None else Some ctor
            with _ -> None

    /// Gets the base type
    let getBaseType (ty: Type): Type option =
        if isNull ty then None
        else
            let baseType = ty.BaseType
            if isNull baseType then None else Some baseType

    /// Gets all interfaces implemented by a type
    let getInterfaces (ty: Type): Type[] =
        if isNull ty then [||]
        else
            try
                ty.GetInterfaces()
            with _ -> [||]

    /// Checks if a type is abstract
    let isAbstract (ty: Type): bool =
        if isNull ty then false
        else ty.IsAbstract

    /// Checks if a type is a generic type
    let isGenericType (ty: Type): bool =
        if isNull ty then false
        else ty.IsGenericType

    /// Checks if a type is a generic type definition
    let isGenericTypeDefinition (ty: Type): bool =
        if isNull ty then false
        else ty.IsGenericTypeDefinition

    /// Gets generic arguments for a generic type
    let getGenericArguments (ty: Type): Type[] =
        if isNull ty then [||]
        else
            try
                ty.GetGenericArguments()
            with _ -> [||]

    /// Gets the generic type definition for a generic type
    let getGenericTypeDefinition (ty: Type): Type option =
        if isNull ty || not (isGenericType ty) then
            None
        else
            try
                Some(ty.GetGenericTypeDefinition())
            with _ -> None

    /// Gets custom attributes of a type
    let getCustomAttributes (ty: Type): Attribute[] =
        if isNull ty then [||]
        else
            try
                ty.GetCustomAttributes() |> Seq.toArray
            with _ -> [||]

    /// Gets custom attributes of a type with a specific attribute type
    let getCustomAttributesOfType (attrType: Type) (ty: Type): Attribute[] =
        if isNull ty || isNull attrType then
            [||]
        else
            try
                ty.GetCustomAttributes(attrType) |> Seq.toArray
            with _ -> [||]

    /// Gets the full name of a type
    let getFullName (ty: Type): string option =
        if isNull ty then
            None
        else
            let fullName = ty.FullName
            if String.isNullOrEmpty fullName then None else Some fullName

    /// Gets the assembly of a type
    let getAssembly (ty: Type): Assembly option =
        if isNull ty then None
        else
            let asm = ty.Assembly
            if isNull asm then None else Some asm

    /// Gets the name of an assembly
    let getAssemblyName (asm: Assembly): AssemblyName option =
        if isNull asm then None
        else
            try
                Some(asm.GetName())
            with _ -> None

    /// Gets types from an assembly
    let getAssemblyTypes (asm: Assembly): Type[] =
        if isNull asm then [||]
        else
            try
                asm.GetTypes()
            with _ -> [||]

    /// Invokes a method on an object
    let invokeMethod (method: MethodInfo) (obj: obj) (args: obj[]): obj option =
        if isNull method then
            None
        else
            try
                Some(method.Invoke(obj, args))
            with _ -> None

    /// Gets the value of a field
    let getFieldValue (field: FieldInfo) (obj: obj): obj option =
        if isNull field then
            None
        else
            try
                Some(field.GetValue(obj))
            with _ -> None

    /// Sets the value of a field
    let setFieldValue (field: FieldInfo) (obj: obj) (value: obj): bool =
        if isNull field then
            false
        else
            try
                field.SetValue(obj, value)
                true
            with _ -> false

    /// Gets the value of a property
    let getPropertyValue (prop: PropertyInfo) (obj: obj): obj option =
        if isNull prop then
            None
        else
            try
                let value = prop.GetValue(obj)
                Some value
            with _ -> None

    /// Sets the value of a property
    let setPropertyValue (prop: PropertyInfo) (obj: obj) (value: obj): bool =
        if isNull prop then
            false
        else
            try
                prop.SetValue(obj, value)
                true
            with _ -> false
