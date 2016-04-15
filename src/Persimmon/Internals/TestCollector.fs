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
  let rec private fixupAndCollectTests (testObject: obj, symbolName: string option) = seq {
    match testObject with
    // For test case:
    | :? TestCase as testCase ->
      // Consume symbol name.
      match symbolName with
      | Some sn -> testCase.trySetSymbolName sn
      | None -> ()
      yield testCase :> TestMetadata
    // For context:
    | :? Context as context ->
      // Consume symbol name.
      match symbolName with
      | Some sn -> context.trySetSymbolName sn
      | None -> ()
      yield! fixupAndCollectTests(context.Children, None)
    // For test objects (sequence, ex: array/list):
    | :? (TestMetadata seq) as tests ->
      yield! tests |> Seq.collect (fun child -> fixupAndCollectTests(child, symbolName))
    // Unknown type, ignored.
    | _ -> ()
  }

  /// Retreive test object via target property, and traverse.
  let private collectTestsFromProperty (p: PropertyInfo) =
    fixupAndCollectTests (p.GetValue(null, null), Some p.Name)
  
  /// Retreive test object via target method, and traverse.
  let private collectTestsFromMethod (m: MethodInfo) =
    fixupAndCollectTests (m.Invoke(null, [||]), Some m.Name)
  
  /// Retreive test object via target type, and traverse.
  let rec collectTests (typ: Type) = seq {
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
      let testCases = collectTests nestedType |> Seq.toArray
      if Array.isEmpty testCases then ()
      else yield Context(nestedType.Name, testCases) :> TestMetadata
  }

[<Sealed>]
type TestCollector() =

  /// Collect test cases from assembly
  let collect targetAssembly =
    targetAssembly
    |> TestCollectorImpl.publicTypes
    |> Seq.map (fun typ -> new Context(typ.FullName, (TestCollectorImpl.collectTests typ |> Seq.toArray)))

  /// Remove contexts and flatten structured test objects.
  let rec flattenTestCase (testMetadata: TestMetadata) = seq {
    match testMetadata with
    | :? Context as context ->
      for child in context.Children do
        yield! flattenTestCase child
    | :? TestCase as testCase -> yield testCase
    | _ -> ()
  }

  /// Collect tests with basic procedure.
  member __.Collect targetAssembly =
    collect targetAssembly |> Seq.toArray

  /// Collect test cases.
  member __.CollectOnlyTestCases targetAssembly =
    collect targetAssembly
    |> Seq.collect flattenTestCase
    |> Seq.toArray

  /// CollectAndCallback collect test cases and callback. (Internal use only)
  member __.CollectAndCallback(targetAssembly, callback: Action<obj>) =
    collect targetAssembly
    |> Seq.collect flattenTestCase
    |> Seq.iter (fun testCase -> callback.Invoke testCase)
