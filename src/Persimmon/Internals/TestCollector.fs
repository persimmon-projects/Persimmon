namespace Persimmon.Internals

open System
open System.Reflection
open Microsoft.FSharp.Collections
open Persimmon

module private TestCollectorImpl =

  let publicTypes (asm: Assembly) =
    asm.GetTypes()
    |> Seq.filter (fun typ -> typ.IsPublic && typ.IsClass && not typ.IsGenericTypeDefinition)

  let private publicNestedTypes (typ: Type) =
    typ.GetNestedTypes()
    |> Seq.filter (fun typ -> typ.IsNestedPublic && typ.IsClass && not typ.IsGenericTypeDefinition)

  /// Traverse test instances recursive.
  /// <param name="partialSuggest">Nested sequence children is true: symbol naming is pseudo.</param>
  let rec private fixupAndCollectTests (testObject: obj, symbolName: string, partialSuggest) = seq {
    match testObject with

    /////////////////////////////////////////////////////
    // For test case:
    | :? TestCase as testCase ->
      // Set symbol name.
      if not partialSuggest then
        testCase.trySetSymbolName symbolName
      yield testCase :> TestMetadata

    /////////////////////////////////////////////////////
    // For context:
    | :? Context as context ->
      // Set symbol name.
      if not partialSuggest then
        context.trySetSymbolName symbolName
      yield context :> TestMetadata

    /////////////////////////////////////////////////////
    // For test objects (sequence, ex: array/list):
    // let tests = [                     --+
    //  test "success test(list)" {        |
    //    ...                              |
    //  }                                  | testObject
    //  test "failure test(list)" {        |
    //    ...                              |
    //  }                                  |
    // ]                                 --+
    | :? (TestMetadata seq) as tests ->
      // Nested children's symbol naming is pseudo, so partialSuggest = true
      let children = tests |> Seq.collect (fun child -> fixupAndCollectTests(child, symbolName, true))
      yield new Context(symbolName, children) :> TestMetadata

    /////////////////////////////////////////////////////
    // Unknown type, ignored.
    | _ -> ()
  }

  /// Retreive test object via target property, and traverse.
  let private collectTestsFromProperty (p: PropertyInfo) =
    fixupAndCollectTests (p.GetValue(null, null), p.Name, false)
  
  /// Retreive test object via target method, and traverse.
  let private collectTestsFromMethod (m: MethodInfo) =
    fixupAndCollectTests (m.Invoke(null, [||]), m.Name, false)
  
  /// Retreive test object via target type, and traverse.
  let rec collectTests (typ: Type) = seq {
    // For properties (value binding):
    yield!
      typ.GetProperties(BindingFlags.Static ||| BindingFlags.Public)
      // Ignore setter only property / indexers
      |> Seq.filter (fun p -> p.CanRead && (p.GetGetMethod() <> null) && (p.GetIndexParameters() |> Array.isEmpty))
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
      else yield new Context(nestedType.Name, testCases) :> TestMetadata
  }

[<Sealed>]
type TestCollector() =

  /// Collect test cases from assembly
  let collect targetAssembly =
    targetAssembly
    |> TestCollectorImpl.publicTypes
    |> Seq.map (fun typ -> new Context(typ.FullName, TestCollectorImpl.collectTests typ))

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
