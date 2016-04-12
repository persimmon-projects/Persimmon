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
  let rec private fixupAndCollectTests (testObject: obj, name: string, declared: MemberInfo, parentContext : ITestMetadata option) =
    seq {
      match testObject with
      // For test case:
      | :? TestCase as testCase ->
        testCase.Fixup(declared.Name, parentContext)
        yield testCase :> ITestMetadata
      // For context:
      | :? Context as context ->
        context.Fixup(declared.Name, parentContext)
        yield! fixupAndCollectTests(context.Children, name, declared, Some (context :> ITestMetadata))
      // For test objects (sequence, ex: array):
      | :? (ITestMetadata seq) as tests ->
        yield! tests |> Seq.collect (fun child -> fixupAndCollectTests(child, name, declared, parentContext))
      // Unknown type, ignored.
      | _ -> ()
    }

  /// Retreive test object via target property, and traverse.
  let private collectTestsFromProperty (p: PropertyInfo, parentContext : ITestMetadata option) =
    fixupAndCollectTests (p.GetValue(null, null), p.Name, p, parentContext)
  
  /// Retreive test object via target method, and traverse.
  let private collectTestsFromMethod (m: MethodInfo, parentContext : ITestMetadata option) =
    fixupAndCollectTests (m.Invoke(null, [||]), m.Name, m, parentContext)
  
  /// Retreive test object via target type, and traverse.
  let rec collectTests (typ: Type, parentContext : ITestMetadata option) =
    seq {
      // For properties (value binding):
      yield!
        typ.GetProperties(BindingFlags.Static ||| BindingFlags.Public)
        |> Seq.collect (fun p -> collectTestsFromProperty(p, parentContext))
      // For methods (function binding):
      yield!
        typ.GetMethods(BindingFlags.Static ||| BindingFlags.Public)
        |> Seq.filter (fun m -> not m.IsSpecialName) // ignore getter methods
        |> Seq.filter (fun m -> m.GetParameters() |> Array.isEmpty)
        |> Seq.collect (fun m -> collectTestsFromMethod(m, parentContext))
      // For nested modules:
      for nestedType in publicNestedTypes typ do
        let testCases = collectTests (nestedType, parentContext)
        if Seq.isEmpty testCases then ()
        else yield Context(Some nestedType.Name, testCases) :> ITestMetadata
    }

[<Sealed>]
type TestCollector() =

  /// Remove contexts and flatten structured test objects.
  let rec flattenTestCase (testMetadata: ITestMetadata) =
    seq {
      match testMetadata with
      | :? Context as context ->
        for child in context.Children do
          yield! flattenTestCase child
      | :? ITestCase as testCase -> yield testCase
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
