namespace Persimmon

open System
open System.Collections
open Microsoft.FSharp.Reflection

module internal PrettyPrinter =

  let rec private printCollections printer (t: Type) (o: IEnumerable) =
    let tmp = ResizeArray()
    let t = t.GetElementType()
    for x in o do tmp.Add(print (t, x))
    tmp |> String.concat "; " |> printer

  // limitation: This function print "null" if Type.Name equals "Object" and object is () or None.
  and print (t: Type, o: obj) =
    match o with
    | null ->
      if t.Name = "Unit" then "()"
      elif t.FullName.StartsWith("Microsoft.FSharp.Core.FSharpOption`1") then "None"
      else "null"
    | :? bool as o -> sprintf "%b" o
    | :? char as o -> sprintf "'%c'" o
    | :? float32 as o ->
      if Single.IsNaN(o) then string o
      elif Single.IsPositiveInfinity(o) then "+∞"
      elif Single.IsNegativeInfinity(o) then "-∞"
      else o.ToString()
    | :? float as o ->
      if Double.IsNaN(o) then string o
      elif Double.IsPositiveInfinity(o) then "+∞"
      elif Double.IsNegativeInfinity(o) then "-∞"
      else o.ToString()
    | :? string as o -> sprintf "\"%s\"" o
    | _ ->
      let t = o.GetType()
      if FSharpType.IsRecord t then
        FSharpValue.GetRecordFields(o)
        |> Array.zip (FSharpType.GetRecordFields(t))
        |> Array.map (fun (p, o) -> (p.GetType(), o) |> print |> sprintf "%s = %s" p.Name)
        |> String.concat "; "
        |> sprintf "{ %s }"
      elif t.IsArray then
        let tmp = ResizeArray()
        let t = t.GetElementType()
        for x in o :?> Array do tmp.Add(print (t, x))
        tmp |> String.concat "; " |> sprintf "[|%s|]"
      elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<_ list> then
        o :?> IEnumerable
        |> printCollections (sprintf "[%s]") t
      elif FSharpType.IsUnion t then
        let u, fs = FSharpValue.GetUnionFields(o, t)
        if Array.isEmpty fs then u.Name
        else
          fs
          |> Array.zip (u.GetFields() |> Array.map (fun p -> p.GetType()))
          |> Array.map print
          |> String.concat ", "
          |> sprintf "%s(%s)" u.Name
      elif FSharpType.IsTuple t then
        FSharpValue.GetTupleFields o
        |> Array.zip (FSharpType.GetTupleElements(t))
        |> Array.map print
        |> String.concat ", "
        |> sprintf "(%s)"
      else
        match o with
        | :? IEnumerable as o ->
          printCollections (sprintf "seq [%s]") t o
        | _ -> string o

  let printAll os = os |> List.map print |> String.concat ", "
