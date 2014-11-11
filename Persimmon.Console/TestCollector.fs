module TestCollector

open System
open System.Reflection
open Persimmon
open RuntimeUtil

let private publicTypes (asm: Assembly) =
  asm.GetTypes()
  |> Seq.filter (fun typ -> typ.IsPublic)

let private publicNestedTypes (typ: Type) =
  typ.GetNestedTypes()
  |> Seq.filter (fun typ -> typ.IsNestedPublic)

let private typedefis<'T>(typ: Type) =
  typ.IsGenericType && typ.GetGenericTypeDefinition() = typedefof<'T>

let (|Type|_|) (matching: Type) (typ: Type) = if typ = matching then Some typ else None
let (|ArrayType|_|) (typ: Type) = if typ.IsArray then Some (typ.GetElementType()) else None
let (|GenericType|_|) (typ: Type) =
  if typ.IsGenericType then
    Some (typ.GetGenericTypeDefinition(), typ.GetGenericArguments())
  else
    None

let persimmonTests f (typ: Type) = seq {
  let testObjType = typeof<TestObject>
  match typ with
  | Type testObjType _ ->
      yield f ()
  | ArrayType elemType when typedefis<TestCase<_>>(elemType) || elemType = typeof<TestObject> ->
      yield! (f (), elemType) |> RuntimeArray.map box
  | GenericType (genTypeDef, _) when genTypeDef = typedefof<TestCase<_>> ->
      yield f ()
  | GenericType (genTypeDef, [| elemType |]) when genTypeDef = typedefof<_ seq> && (typedefis<TestCase<_>>(elemType) || elemType = typeof<TestObject>) ->
      yield! (f (), elemType) |> RuntimeSeq.map box
  | GenericType (genTypeDef, [| elemType |]) when genTypeDef = typedefof<_ list> && (typedefis<TestCase<_>>(elemType) || elemType = typeof<TestObject>) ->
      yield! (f (), elemType) |> RuntimeList.map box
  | _ -> ()
}

let persimmonTestProps (p: PropertyInfo) =
  persimmonTests (fun () -> p.GetValue(null) :?> TestObject) p.PropertyType
let persimmonTestMethods (m: MethodInfo) =
  persimmonTests (fun () -> m.Invoke(null, [||]) :?> TestObject) m.ReturnType

let rec private testObjects (typ: Type) =
  seq {
    yield!
      typ.GetProperties(BindingFlags.Static ||| BindingFlags.Public)
      |> Seq.collect persimmonTestProps
    yield!
      typ.GetMethods(BindingFlags.Static ||| BindingFlags.Public)
      |> Seq.filter (fun m -> not m.IsSpecialName) // ignore getter methods
      |> Seq.filter (fun m -> m.GetParameters() |> Array.isEmpty)
      |> Seq.collect persimmonTestMethods
    for nestedType in publicNestedTypes typ do
      let objs = testObjects nestedType
      if Seq.isEmpty objs then ()
      else yield Context(nestedType.Name, objs |> Seq.toList) :> TestObject
  }

let collectRootTestObjects (asms: Assembly list) =
  asms
  |> Seq.collect publicTypes
  |> Seq.collect testObjects