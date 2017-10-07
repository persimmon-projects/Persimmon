namespace Persimmon.Tests

open System
open Persimmon
open Persimmon.ActivePatterns
open UseTestNameByReflection
open Helper

module TestCollectorTest =
  type private Dummy() = class end

  let rec findContext names (acc: Context) =
    match names with
    | [] -> acc
    | name :: rest ->
      acc.Children
      |> Seq.pick (function
        | Context ctx when ctx.DisplayName = name -> Some ctx
        | _ -> None)
      |> findContext rest

  let ``should read category`` = parameterize {
    source [
      [], "noCategory", []
      [], "oneCategory", [ "A" ]
      [], "manyCategory", [ "A"; "B" ]
      [], "immutable", [ "A"; "C" ]
      [], "parameterizeTests(1)[0]", [ "A" ]
      [], "notTransfer", []
      [ "WithCategoryAttribute" ], "noCategory", [ "ModuleCategory" ]
      [ "WithCategoryAttribute" ], "oneCategory", [ "a"; "ModuleCategory" ]
      [ "WithCategoryAttribute" ], "immutable", [ "a"; "c"; "ModuleCategory" ]
      [ "WithCategoryAttribute" ], "parameterizeTests(1)[0]", [ "a"; "ModuleCategory" ]
      [ "WithCategoryAttribute"; "NestedModule" ], "test1", [ "ModuleCategory" ]
      [ "WithCategoryAttribute"; "NestedModule2" ], "test1", [ "ModuleCategory"; "ModuleCategory2" ]
      [ "Multiple" ], "test1", [ "A"; "B"; "C" ]
    ]
    run (fun (contextNames, testName, expected) -> test {
      let moduleType = Internals.Runtime.getModule<Dummy> "Persimmon.Tests.TestCollectorTest+ForCategoryTest"
      let tests =
        Internals.TestCollectorImpl.collectTestsAsContext moduleType
        |> Option.map (unbox<Context> >> findContext contextNames)
        |> function Some ctx -> Internals.TestCollectorImpl.flattenTestCase ctx | None -> Seq.empty
      let actual = tests |> Seq.find (fun x -> x.DisplayName = testName)
      do! (Array.sort actual.Categories) |> assertEquals (Array.sort (Array.ofList expected))
    })
  } 

  module ForCategoryTest =
    // These are dummy test for reading category test.
    let noCategory = test { do! pass() }
    let oneCategory = test { do! pass() } |> category "A"
    let manyCategory = test { do! pass() } |> category "A" |> category "B"

    let immutable = oneCategory |> category "C"

    let parameterizeTests = parameterize {
      source [ 1..2 ]
      run (fun n -> test { do! pass() } |> category "A")
    }

    let notTransfer = test {
      let x = (test { return 3 } |> category "D")
      do! pass()
    }

    [<Category("ModuleCategory")>]
    module WithCategoryAttribute =
      let noCategory = test { do! pass() }
      let oneCategory = test { do! pass() } |> category "a"
      let manyCategory = test { do! pass() } |> category "a" |> category "b"

      let immutable = oneCategory |> category "c"

      let parameterizeTests = parameterize {
        source [ 1..2 ]
        run (fun n -> test { do! pass() } |> category "a")
      }

      module NestedModule =
        let test1 = test { do! pass() }

      [<Category("ModuleCategory2")>]
      module NestedModule2 =
        let test1 = test { do! pass() }

    [<Category("A", "B"); Category("C")>]
    module Multiple =
      let test1 = test { do! pass() }