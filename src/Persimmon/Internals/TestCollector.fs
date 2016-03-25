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

  /// Traverse test object instances recursive.
  let rec persimmonTests (testObject: obj, name: string, declared: MemberInfo) =
    seq {
      match testObject with
      // For test case:
      | :? ITestCase as testCase ->
        yield
          testCase.CreateAdditionalMetadataIfNeed(declared.Name, declared) :> ITestObject
      // For test objects (sequence, ex: array):
      | :? (ITestObject seq) as testObjects ->
        yield!
          testObjects
          |> Seq.collect (fun testObject -> persimmonTests(testObject, name, declared))
      // For context:
      | :? Context as context ->
        // TODO: NOT collect children, save context structures.
        let children = context.CreateAdditionalMetadataIfNeed(declared.Name, declared, None).Children
        yield!
          persimmonTests(children, name, declared)
      // Unknown type, ignored.
      | _ -> ()
    }

  /// Retreive test object via target property, and traverse.
  let persimmonTestProps (p: PropertyInfo) =
    persimmonTests (p.GetValue(null, null), p.Name, p)
  
  /// Retreive test object via target method, and traverse.
  let persimmonTestMethods (m: MethodInfo) =
    persimmonTests (m.Invoke(null, [||]), m.Name, m)
  
  /// Retreive test object via target type, and traverse.
  let rec testObjects (typ: Type) =
    seq {
      // For properties (value binding):
      yield!
        typ.GetProperties(BindingFlags.Static ||| BindingFlags.Public)
        |> Seq.collect persimmonTestProps
      // For methods (function binding):
      yield!
        typ.GetMethods(BindingFlags.Static ||| BindingFlags.Public)
        |> Seq.filter (fun m -> not m.IsSpecialName) // ignore getter methods
        |> Seq.filter (fun m -> m.GetParameters() |> Array.isEmpty)
        |> Seq.collect persimmonTestMethods
      // For nested modules:
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
