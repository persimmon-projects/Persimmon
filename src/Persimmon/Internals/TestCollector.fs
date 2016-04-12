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
  let rec private fixupAndCollectTests (testObject: obj, name: string, declared: MemberInfo, parent : TestMetadata option) =
    seq {
      match testObject with
      // For test case:
      | :? TestCase as testCase ->
        testCase.Fixup(declared.Name, parent)
        yield testCase :> TestMetadata
      // For context:
      | :? Context as context ->
        context.Fixup(declared.Name, parent)
        yield! fixupAndCollectTests(context.Children, name, declared, Some (context :> TestMetadata))
      // For test objects (sequence, ex: array):
      | :? (TestMetadata seq) as tests ->
        yield! tests |> Seq.collect (fun child -> fixupAndCollectTests(child, name, declared, parent))
      // Unknown type, ignored.
      | _ -> ()
    }

  /// Retreive test object via target property, and traverse.
  let private collectTestsFromProperty (p: PropertyInfo, parent : TestMetadata option) =
    fixupAndCollectTests (p.GetValue(null, null), p.Name, p, parent)
  
  /// Retreive test object via target method, and traverse.
  let private collectTestsFromMethod (m: MethodInfo, parent : TestMetadata option) =
    fixupAndCollectTests (m.Invoke(null, [||]), m.Name, m, parent)
  
  /// Retreive test object via target type, and traverse.
  let rec collectTests (typ: Type, parent : TestMetadata option) =
    seq {
      // For properties (value binding):
      yield!
        typ.GetProperties(BindingFlags.Static ||| BindingFlags.Public)
        |> Seq.collect (fun p -> collectTestsFromProperty(p, parent))
      // For methods (function binding):
      yield!
        typ.GetMethods(BindingFlags.Static ||| BindingFlags.Public)
        |> Seq.filter (fun m -> not m.IsSpecialName) // ignore getter methods
        |> Seq.filter (fun m -> m.GetParameters() |> Array.isEmpty)
        |> Seq.collect (fun m -> collectTestsFromMethod(m, parent))
      // For nested modules:
      for nestedType in publicNestedTypes typ do
        let testCases = collectTests (nestedType, parent)
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
      |> Seq.collect (fun t -> TestCollectorImpl.collectTests(t, None))

  /// Collect test cases.
  member this.CollectOnlyTestCases(target: Assembly) =
    target |> this.Collect
      |> Seq.collect flattenTestCase

  /// CollectAndCallback collect test cases and callback. (Internal use only)
  member this.CollectAndCallback(target: Assembly, callback: Action<obj>) =
    target |> this.CollectOnlyTestCases
      |> Seq.iter (fun testCase -> callback.Invoke(testCase))
