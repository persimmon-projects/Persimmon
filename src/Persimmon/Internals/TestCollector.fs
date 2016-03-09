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

  let persimmonTests (f: unit -> obj) (typ: Type) (declaredMember: MemberInfo) name = seq {
    let testObjType = typeof<ITestCase>
    match typ with
    | SubTypeOf testObjType _ ->
        yield (f () :?> ITestCase).CreateAdditionalMetadataIfNeed(name, declaredMember)
    | ArrayType elemType when typedefis<TestCase<_>>(elemType) || elemType = typeof<TestObject> ->
        yield! (f (), elemType) |> RuntimeArray.map (fun x -> (x :?> ITestCase).CreateAdditionalMetadataIfNeed(name, declaredMember) |> box)
    | GenericType (genTypeDef, _) when genTypeDef = typedefof<TestCase<_>> ->
        yield (f () :?> ITestCase).CreateAdditionalMetadataIfNeed(name, declaredMember)
    | GenericType (genTypeDef, [| elemType |]) when genTypeDef = typedefof<_ seq> && (typedefis<TestCase<_>>(elemType) || elemType = typeof<TestObject>) ->
        yield! (f (), elemType) |> RuntimeSeq.map (fun x -> (x :?> ITestCase).CreateAdditionalMetadataIfNeed(name, declaredMember) |> box)
    | GenericType (genTypeDef, [| elemType |]) when genTypeDef = typedefof<_ list> && (typedefis<TestCase<_>>(elemType) || elemType = typeof<TestObject>) ->
        yield! (f (), elemType) |> RuntimeList.map (fun x -> (x :?> ITestCase).CreateAdditionalMetadataIfNeed(name, declaredMember) |> box)
    | _ -> ()
  }

  let persimmonTestProps (p: PropertyInfo) =
    persimmonTests (fun () -> p.GetValue(null, null)) p.PropertyType (p.GetGetMethod()) p.Name
  let persimmonTestMethods (m: MethodInfo) =
    persimmonTests (fun () -> m.Invoke(null, [||])) m.ReturnType m m.Name

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
        else yield Context(nestedType.Name, nestedType.GetConstructors().[0], objs |> Seq.toList) // TODO: Is constructor exactly defined?
          :> ITestObject
    }

[<Sealed>]
type TestCollector() =
  
  /// Recursively apply declared type for Context.
  let rec fixupDeclaredType (testObject:ITestObject) =
    match testObject with
    | :? Context as context ->
        context.CreateAdditionalMetadataIfNeed(
          context.Name.Value,
          testObject.DeclaredMember.Value,
          Some fixupDeclaredType) :> ITestObject
    | _ -> testObject

  /// Remove contexts and flatten structured test objects.
  let rec flattenTestCase (testObject:ITestObject) =
    seq {
      match testObject with
      | :? Context as context ->
        for child in context.Children do
          // TODO: Apply context-path into FullName?
          yield! flattenTestCase child
      | :? ITestCase as testCase -> yield testCase
      | _ -> ()
    }

  /// Collect test objects with basic procedure.
  member __.Collect(target: Assembly) =
    target |> TestCollectorImpl.publicTypes
      |> Seq.collect TestCollectorImpl.testObjects
      |> Seq.map fixupDeclaredType

  /// Collect test cases.
  member this.CollectOnlyTestCases(target: Assembly) =
    target |> this.Collect
      |> Seq.collect flattenTestCase

  /// CollectAndCallback collect test cases and callback. (Internal use only)
  member this.CollectAndCallback(target: Assembly, callback: Action<obj>) =
    target |> this.CollectOnlyTestCases
      |> Seq.iter (fun testCase -> callback.Invoke(testCase))
