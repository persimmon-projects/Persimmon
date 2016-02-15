namespace Persimmon.Internals

open System
open System.Reflection
open Microsoft.FSharp.Collections

module private TestCollectorImpl =

  open Persimmon

  let publicTypes (asm: Assembly) =
    asm.GetTypes()
    |> Seq.filter (fun typ -> typ.IsPublic)

  let publicNestedTypes (typ: Type) =
    typ.GetNestedTypes()
    |> Seq.filter (fun typ -> typ.IsNestedPublic)

  let typedefis<'T>(typ: Type) =
    typ.IsGenericType && typ.GetGenericTypeDefinition() = typedefof<'T>

  let (|SubTypeOf|_|) (matching: Type) (typ: Type) =
    if matching.IsAssignableFrom(typ) then Some typ else None
  let (|ArrayType|_|) (typ: Type) = if typ.IsArray then Some (typ.GetElementType()) else None
  let (|GenericType|_|) (typ: Type) =
    if typ.IsGenericType then
      Some (typ.GetGenericTypeDefinition(), typ.GetGenericArguments())
    else
      None

  let persimmonTests (f: unit -> obj) (typ: Type) name = seq {
    let testObjType = typeof<TestObject>
    match typ with
    | SubTypeOf testObjType _ ->
        yield (f () :?> TestObject).SetNameIfNeed(name)
    | ArrayType elemType when typedefis<TestCase<_>>(elemType) || elemType = typeof<TestObject> ->
        yield! (f (), elemType) |> RuntimeArray.map (fun x -> (x :?> TestObject).SetNameIfNeed(name) |> box)
    | GenericType (genTypeDef, _) when genTypeDef = typedefof<TestCase<_>> ->
        yield (f () :?> TestObject).SetNameIfNeed(name)
    | GenericType (genTypeDef, [| elemType |]) when genTypeDef = typedefof<_ seq> && (typedefis<TestCase<_>>(elemType) || elemType = typeof<TestObject>) ->
        yield! (f (), elemType) |> RuntimeSeq.map (fun x -> (x :?> TestObject).SetNameIfNeed(name) |> box)
    | GenericType (genTypeDef, [| elemType |]) when genTypeDef = typedefof<_ list> && (typedefis<TestCase<_>>(elemType) || elemType = typeof<TestObject>) ->
        yield! (f (), elemType) |> RuntimeList.map (fun x -> (x :?> TestObject).SetNameIfNeed(name) |> box)
    | _ -> ()
  }

  let persimmonTestProps (p: PropertyInfo) =
    persimmonTests (fun () -> p.GetValue(null, null)) p.PropertyType p.Name
  let persimmonTestMethods (m: MethodInfo) =
    persimmonTests (fun () -> m.Invoke(null, [||])) m.ReturnType m.Name

  let rec testObjects (typ: Type) =
    seq {
      yield!
        typ.GetProperties(BindingFlags.Static ||| BindingFlags.Public)
        |> Seq.collect persimmonTestProps
        |> Seq.map (fun x -> (typ, x))
      yield!
        typ.GetMethods(BindingFlags.Static ||| BindingFlags.Public)
        |> Seq.filter (fun m -> not m.IsSpecialName) // ignore getter methods
        |> Seq.filter (fun m -> m.GetParameters() |> Array.isEmpty)
        |> Seq.collect persimmonTestMethods
        |> Seq.map (fun x -> (typ, x))
      for nestedType in publicNestedTypes typ do
        let objs = testObjects nestedType |> Seq.map snd
        if Seq.isEmpty objs then ()
        else yield (nestedType, Context(nestedType.Name, objs |> Seq.toList) :> TestObject)
    }

[<Sealed>]
type TestCollector() =
  member __.Run(target: Assembly) =
#if DEBUG
    let currentAppDomain = AppDomain.CurrentDomain
    let assembly = Assembly.GetExecutingAssembly()
    let currentFSharpFuncType = typedefof<FSharpFunc<obj, obj>>
    let currentFSharpCore = currentFSharpFuncType.Assembly
#endif
    target |> TestCollectorImpl.publicTypes
      |> Seq.collect TestCollectorImpl.testObjects
      |> Seq.map (fun (t, testObject) -> testObject)

  member __.RunAndMarshal(target: Assembly, f: Action<obj>) =
#if DEBUG
    let currentAppDomain = AppDomain.CurrentDomain
    let assembly = Assembly.GetExecutingAssembly()
    let currentFSharpFuncType = typedefof<FSharpFunc<obj, obj>>
    let currentFSharpCore = currentFSharpFuncType.Assembly
#endif
    target |> TestCollectorImpl.publicTypes
      |> Seq.collect TestCollectorImpl.testObjects
      |> Seq.map (TestCase.ofTestObject target.FullName)
      |> Seq.iter (fun testCase -> f.Invoke(testCase))
