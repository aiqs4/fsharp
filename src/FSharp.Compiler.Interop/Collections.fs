// Copyright (c) Sascha Dewald. See https://github.com/fsharp-wasm/fsharp

namespace FSharp.Compiler.Interop

/// Nullability-safe wrappers for System.Collections.Generic operations
module Collections =
    open System
    open System.Collections.Generic

    /// Creates a new generic list
    let createList<'T> (capacity: int option): List<'T> =
        match capacity with
        | Some cap when cap > 0 -> List<'T>(cap)
        | _ -> List<'T>()

    /// Gets the count of items in a collection
    let getCount (collection: ICollection<'T>): int =
        if isNull collection then 0
        else collection.Count

    /// Adds an item to a list safely
    let addToList (list: List<'T>) (item: 'T): bool =
        if isNull list then false
        else
            try
                list.Add(item)
                true
            with _ -> false

    /// Removes an item from a list safely
    let removeFromList (list: List<'T>) (item: 'T): bool =
        if isNull list then false
        else
            try
                list.Remove(item)
            with _ -> false

    /// Clears all items from a list
    let clearList (list: List<'T>): bool =
        if isNull list then false
        else
            try
                list.Clear()
                true
            with _ -> false

    /// Checks if a list contains an item
    let containsInList (list: List<'T>) (item: 'T): bool =
        if isNull list then false
        else
            try
                list.Contains(item)
            with _ -> false

    /// Gets an item at index from a list
    let getListItem (list: List<'T>) (index: int): 'T option =
        if isNull list || index < 0 || index >= list.Count then
            None
        else
            try
                Some(list.[index])
            with _ -> None

    /// Sets an item at index in a list
    let setListItem (list: List<'T>) (index: int) (item: 'T): bool =
        if isNull list || index < 0 || index >= list.Count then
            false
        else
            try
                list.[index] <- item
                true
            with _ -> false

    /// Converts list to array
    let listToArray (list: List<'T>): 'T[] =
        if isNull list then [||]
        else
            try
                list.ToArray()
            with _ -> [||]

    /// Creates a list from an enumerable
    let createListFromEnumerable (enumerable: IEnumerable<'T>): List<'T> =
        if isNull enumerable then
            List<'T>()
        else
            try
                List<'T>(enumerable)
            with _ -> List<'T>()

    /// Creates a new generic dictionary
    let createDictionary<'K, 'V when 'K: equality and 'K: not null> (capacity: int option): Dictionary<'K, 'V> =
        match capacity with
        | Some cap when cap > 0 -> Dictionary<'K, 'V>(cap)
        | _ -> Dictionary<'K, 'V>()

    /// Adds a key-value pair to a dictionary safely
    let addToDictionary (dict: Dictionary<'K, 'V>) (key: 'K) (value: 'V): bool when 'K: equality and 'K: not null =
        if isNull dict || isNull (box key) then false
        else
            try
                dict.Add(key, value)
                true
            with _ -> false

    /// Tries to add a key-value pair, returning success
    let tryAddToDictionary (dict: Dictionary<'K, 'V>) (key: 'K) (value: 'V): bool when 'K: equality =
        if isNull dict || isNull (box key) then false
        else
            try
                dict.Add(key, value)
                true
            with _ -> false

    /// Removes a key from a dictionary
    let removeFromDictionary (dict: Dictionary<'K, 'V>) (key: 'K): bool when 'K: equality =
        if isNull dict || isNull (box key) then false
        else
            try
                dict.Remove(key)
            with _ -> false

    /// Clears all items from a dictionary
    let clearDictionary (dict: Dictionary<'K, 'V>): bool when 'K: equality =
        if isNull dict then false
        else
            try
                dict.Clear()
                true
            with _ -> false

    /// Checks if a dictionary contains a key
    let containsKeyInDictionary (dict: Dictionary<'K, 'V>) (key: 'K): bool when 'K: equality =
        if isNull dict || isNull (box key) then false
        else
            try
                dict.ContainsKey(key)
            with _ -> false

    /// Gets a value by key from a dictionary
    let tryGetFromDictionary (dict: Dictionary<'K, 'V>) (key: 'K): 'V option when 'K: equality =
        if isNull dict || isNull (box key) then
            None
        else
            try
                let value = ref Unchecked.defaultof<'V>
                if dict.TryGetValue(key, value) then
                    Some(!value)
                else
                    None
            with _ -> None

    /// Gets a value by key, returning a default if not found
    let getFromDictionary (dict: Dictionary<'K, 'V>) (key: 'K) (defaultValue: 'V): 'V when 'K: equality =
        if isNull dict || isNull (box key) then
            defaultValue
        else
            try
                let value = ref Unchecked.defaultof<'V>
                if dict.TryGetValue(key, value) then
                    !value
                else
                    defaultValue
            with _ -> defaultValue

    /// Gets dictionary keys as array
    let getDictionaryKeys (dict: Dictionary<'K, 'V>): 'K[] when 'K: equality =
        if isNull dict then [||]
        else
            try
                let keys = dict.Keys
                if isNull keys then [||]
                else
                    let arr = Array.zeroCreate keys.Count
                    keys.CopyTo(arr, 0)
                    arr
            with _ -> [||]

    /// Gets dictionary values as array
    let getDictionaryValues (dict: Dictionary<'K, 'V>): 'V[] when 'K: equality =
        if isNull dict then [||]
        else
            try
                let values = dict.Values
                if isNull values then [||]
                else
                    let arr = Array.zeroCreate values.Count
                    values.CopyTo(arr, 0)
                    arr
            with _ -> [||]

    /// Converts dictionary to list of key-value pairs
    let dictionaryToList (dict: Dictionary<'K, 'V>): (('K * 'V) list) when 'K: equality =
        if isNull dict then []
        else
            try
                dict
                |> Seq.map (fun kvp -> (kvp.Key, kvp.Value))
                |> List.ofSeq
            with _ -> []

    /// Creates a new HashSet
    let createHashSet<'T> (capacity: int option): HashSet<'T> =
        match capacity with
        | Some cap when cap > 0 -> HashSet<'T>(cap)
        | _ -> HashSet<'T>()

    /// Adds an item to a HashSet
    let addToHashSet (set: HashSet<'T>) (item: 'T): bool =
        if isNull set then false
        else
            try
                set.Add(item)
            with _ -> false

    /// Removes an item from a HashSet
    let removeFromHashSet (set: HashSet<'T>) (item: 'T): bool =
        if isNull set then false
        else
            try
                set.Remove(item)
            with _ -> false

    /// Checks if a HashSet contains an item
    let containsInHashSet (set: HashSet<'T>) (item: 'T): bool =
        if isNull set then false
        else
            try
                set.Contains(item)
            with _ -> false

    /// Clears all items from a HashSet
    let clearHashSet (set: HashSet<'T>): bool =
        if isNull set then false
        else
            try
                set.Clear()
                true
            with _ -> false

    /// Gets the count of items in a HashSet
    let getHashSetCount (set: HashSet<'T>): int =
        if isNull set then 0
        else set.Count

    /// Converts HashSet to array
    let hashSetToArray (set: HashSet<'T>): 'T[] =
        if isNull set then [||]
        else
            try
                let arr = Array.zeroCreate set.Count
                set.CopyTo(arr)
                arr
            with _ -> [||]

    /// Creates a new SortedDictionary
    let createSortedDictionary<'K, 'V when 'K: not null> (): SortedDictionary<'K, 'V> =
        SortedDictionary<'K, 'V>()

    /// Adds to a SortedDictionary
    let addToSortedDictionary (dict: SortedDictionary<'K, 'V>) (key: 'K) (value: 'V): bool when 'K: comparison and 'K: not null =
        if isNull dict || isNull (box key) then false
        else
            try
                dict.Add(key, value)
                true
            with _ -> false

    /// Removes from a SortedDictionary
    let removeFromSortedDictionary (dict: SortedDictionary<'K, 'V>) (key: 'K): bool when 'K: comparison =
        if isNull dict || isNull (box key) then false
        else
            try
                dict.Remove(key)
            with _ -> false

    /// Gets an enumerator from a collection safely
    let getEnumerator<'T> (collection: IEnumerable<'T>): IEnumerator<'T> option =
        if isNull collection then
            None
        else
            try
                Some(collection.GetEnumerator())
            with _ -> None

    /// Moves to next item in enumerator and gets current
    let tryGetNext<'T> (enumerator: IEnumerator<'T>): 'T option =
        if isNull enumerator then
            None
        else
            try
                if enumerator.MoveNext() then
                    Some(enumerator.Current)
                else
                    None
            with _ -> None
