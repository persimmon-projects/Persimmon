namespace Persimmon.Internals

open System
open System.Reflection
open Microsoft.FSharp.Collections

open Persimmon

module private TestCollectorImpl =

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

  let persimmonTests (f: unit -> obj) (typ: Type) (declaredType: Type) name = seq {
    let testObjType = typeof<ITestCase>
    match typ with
    | SubTypeOf testObjType _ ->
        yield (f () :?> ITestCase).CreateAdditionalMetadataIfNeed(name, declaredType)
    | ArrayType elemType when typedefis<TestCase<_>>(elemType) || elemType = typeof<TestObject> ->
        yield! (f (), elemType) |> RuntimeArray.map (fun x -> (x :?> ITestCase).CreateAdditionalMetadataIfNeed(name, declaredType) |> box)
    | GenericType (genTypeDef, _) when genTypeDef = typedefof<TestCase<_>> ->
        yield (f () :?> ITestCase).CreateAdditionalMetadataIfNeed(name, declaredType)
    | GenericType (genTypeDef, [| elemType |]) when genTypeDef = typedefof<_ seq> && (typedefis<TestCase<_>>(elemType) || elemType = typeof<TestObject>) ->
        yield! (f (), elemType) |> RuntimeSeq.map (fun x -> (x :?> ITestCase).CreateAdditionalMetadataIfNeed(name, declaredType) |> box)
    | GenericType (genTypeDef, [| elemType |]) when genTypeDef = typedefof<_ list> && (typedefis<TestCase<_>>(elemType) || elemType = typeof<TestObject>) ->
        yield! (f (), elemType) |> RuntimeList.map (fun x -> (x :?> ITestCase).CreateAdditionalMetadataIfNeed(name, declaredType) |> box)
    | _ -> ()
  }

  let persimmonTestProps (p: PropertyInfo) =
    persimmonTests (fun () -> p.GetValue(null, null)) p.PropertyType p.DeclaringType p.Name
  let persimmonTestMethods (m: MethodInfo) =
    persimmonTests (fun () -> m.Invoke(null, [||])) m.ReturnType m.DeclaringType m.Name

  let rec testObjects (typ: Type) =
    seq {
      yield!
        typ.GetProperties(BindingFlags.Static ||| BindingFlags.Public)
        |> Seq.collect persimmonTestProps
        |> Seq.map (fun testCase -> testCase :> ITestObject)
      yield!
        typ.GetMethods(BindingFlags.Static ||| BindingFlags.Public)
        |> Seq.filter (fun m -> not m.IsSpecialName) // ignore getter methods
        |> Seq.filter (fun m -> m.GetParameters() |> Array.isEmpty)
        |> Seq.collect persimmonTestMethods
        |> Seq.map (fun testCase -> testCase :> ITestObject)
      for nestedType in publicNestedTypes typ do
        let objs = testObjects nestedType
        if Seq.isEmpty objs then ()
        else yield Context(nestedType.Name, nestedType, objs |> Seq.toList) :> ITestObject
    }

[<Sealed>]
type TestCollector() =
  
  let rec fixupDeclaredType (testObject:ITestObject) =
    match testObject with
    | :? Context as context ->
        context.CreateAdditionalMetadataIfNeed(
          context.Name.Value,
          testObject.DeclaredType.Value,
          Some fixupDeclaredType) :> ITestObject
    | _ -> testObject

  let rec flattenTestCase (testObject:ITestObject) =
    seq {
      match testObject with
      | :? Context as context ->
        for child in context.Children do
          yield! flattenTestCase child
      | :? ITestCase as testCase -> yield testCase
      | _ -> ()
    }

  member __.Collect(target: Assembly) =
    target |> TestCollectorImpl.publicTypes
      |> Seq.collect TestCollectorImpl.testObjects
      |> Seq.map fixupDeclaredType

  member __.CollectOnlyTestCases(target: Assembly) =
    target |> TestCollectorImpl.publicTypes
      |> Seq.collect TestCollectorImpl.testObjects
      |> Seq.map fixupDeclaredType
      |> Seq.collect flattenTestCase

  /// CollectAndCallback is safe-serializable-types runner method.
  member this.CollectAndCallback(target: Assembly, callback: Action<obj>) =
    target |> this.CollectOnlyTestCases
      |> Seq.iter (fun testCase -> callback.Invoke(testCase))
