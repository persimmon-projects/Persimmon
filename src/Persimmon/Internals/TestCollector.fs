namespace Persimmon.Internals

open System
open System.Reflection
open Microsoft.FSharp.Collections
open Persimmon

module private TestCollectorImpl =

  let publicTypes (asm: Assembly) =
    asm.GetTypes()
    |> Seq.filter (fun typ -> typ.IsPublic)

  let private publicNestedTypes (typ: Type) =
    typ.GetNestedTypes()
    |> Seq.filter (fun typ -> typ.IsNestedPublic)

  /// Traverse test instances recursive.
  let rec private fixupAndCollectTests (testObject: obj, name: string, declared: MemberInfo) =
    seq {
      match testObject with
      // For test case:
      | :? TestCase as testCase ->
        testCase.fixupNaming declared.Name
        yield testCase :> TestMetadata
      // For context:
      | :? Context as context ->
        context.fixupNaming declared.Name
        yield! fixupAndCollectTests(context.Children, name, declared)
      // For test objects (sequence, ex: array):
      | :? (TestMetadata seq) as tests ->
        yield! tests |> Seq.collect (fun child -> fixupAndCollectTests(child, name, declared))
      // Unknown type, ignored.
      | _ -> ()
    }

  /// Retreive test object via target property, and traverse.
  let private collectTestsFromProperty (p: PropertyInfo) =
    fixupAndCollectTests (p.GetValue(null, null), p.Name, p)
  
  /// Retreive test object via target method, and traverse.
  let private collectTestsFromMethod (m: MethodInfo) =
    fixupAndCollectTests (m.Invoke(null, [||]), m.Name, m)
  
  /// Retreive test object via target type, and traverse.
  let rec collectTests (typ: Type) =
    seq {
      // For properties (value binding):
      yield!
        typ.GetProperties(BindingFlags.Static ||| BindingFlags.Public)
        |> Seq.collect collectTestsFromProperty
      // For methods (function binding):
      yield!
        typ.GetMethods(BindingFlags.Static ||| BindingFlags.Public)
        // Ignore getter methods / open generic methods / method has parameters
        |> Seq.filter (fun m -> not m.IsSpecialName && not m.IsGenericMethodDefinition && (m.GetParameters() |> Array.isEmpty))
        |> Seq.collect collectTestsFromMethod
      // For nested modules:
      for nestedType in publicNestedTypes typ do
        let testCases = collectTests nestedType
        if Seq.isEmpty testCases then ()
        else yield Context(nestedType.Name, testCases) :> TestMetadata
    }

[<Sealed>]
type TestCollector() =

  /// Remove contexts and flatten structured test objects.
  let rec flattenTestCase (testMetadata: TestMetadata) =
    seq {
      match testMetadata with
      | :? Context as context ->
        for child in context.Children do
          yield! flattenTestCase child
      | :? TestCase as testCase -> yield testCase
      | _ -> ()
    }

  /// Collect tests with basic procedure.
  member __.Collect(target: Assembly) =
    target |> TestCollectorImpl.publicTypes
      |> Seq.collect TestCollectorImpl.collectTests

  /// Collect test cases.
  member this.CollectOnlyTestCases(target: Assembly) =
    target |> this.Collect
      |> Seq.collect flattenTestCase

  /// CollectAndCallback collect test cases and callback. (Internal use only)
  member this.CollectAndCallback(target: Assembly, callback: Action<obj>) =
    target |> this.CollectOnlyTestCases
      |> Seq.iter (fun testCase -> callback.Invoke(testCase))
