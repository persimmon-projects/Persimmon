module Persimmon.Runner.Tests.TestFilterTest

open Persimmon
open UseTestNameByReflection
open Persimmon.Runner

let ``should filter testcase`` = parameterize {
  source [
    [ "A" ], [], [ "A" ], true
    [ "A" ], [], [ "B" ], false
    [ "A" ], [], [], false

    [ "A"; "B" ], [], [ "A"; "B" ], true
    [ "A"; "B" ], [], [ "A"; ], false

    [], [ "A" ], [ "A"; "B" ], false
    [], [ "A" ], [ "B" ], true
    [], [ "A" ], [], true

    [], [ "A"; "B" ], [ "A" ], false
    [], [ "A"; "B" ], [ "B" ], false
    [], [ "A"; "B" ], [ "C" ], true

    [ "A" ], [ "B" ], [ "A" ], true
    [ "A" ], [ "B" ], [ "A"; "C" ], true
    [ "A" ], [ "B" ], [ "A"; "B"; "C" ], false
    [ "A" ], [ "B" ], [ "C" ], false

    [], [], [], true
    [], [], [ "A" ], true
  ]
  run (fun (includes, excludes, categories, expected) -> test {
    let filter = TestFilter.make { IncludeCategories = Set.ofList includes; ExcludeCategories = Set.ofList excludes }
    let testCase = TestCase.makeDone None (Seq.ofList categories) [] (Passed ())
    let actual = filter testCase
    do! actual |> assertEquals expected
  })
}