namespace Persimmon.Tests

open Persimmon
open Helper

module PersimmonTest =

  let ``'pass' function should always pass`` =
    test "'pass' function should always pass" {
      return! pass 1
    }
    |> shouldPassed 1

  let ``'fail' function should always fail`` =
    let msg = "always fail"
    test "'fail' function should always fail" {
      return! fail msg
    }
    |> shouldNotPassed (NonEmptyList.singleton msg)

  let ``all unit type assertion should run`` =
    test "all unit type assertion should run" {
      do! assertEquals 1 2
      do! assertEquals 2 2
      do! assertEquals 3 4
    }
    |> shouldNotPassed<unit> (NonEmptyList.make "Expect: 1\nActual: 2" [ "Expect: 3\nActual: 4" ])

  let table = Map.ofList [("x", "y"); ("y", "z"); ("z", "other")]

  let ``Persimmon test should be able to compose`` =
    let test1 = test "pass" {
      let value = table |> Map.find "x"
      do! assertEquals "y" value
      return value
    }
    let test2 = test "more pass" {
      let! res = test1
      let value = table |> Map.find res
      do! assertEquals "z" value
      return value
    }
    let test3 = test "fail" {
      let! res = test2
      let value = table |> Map.find res
      do! assertEquals "x" value
      return value
    }
    let test4 = test "more fail" {
      let! res = test3
      let value = table |> Map.find res
      do! assertEquals "y" value // not execute
      return value
    }

    test "Persimmon test should be able to compose" {
      do! test1 |> shouldPassed "y"
      do! test2 |> shouldPassed "z"
      do! test3 |> shouldNotPassed (NonEmptyList.singleton "Expect: \"x\"\nActual: \"other\"")
      do! test4 |> shouldNotPassed (NonEmptyList.singleton "Expect: \"x\"\nActual: \"other\"")
    }

  let ``parameterize tests should be able to run`` =
    let innerTests =
      parameterize {
        source [
          (1, 1)
          (1, 2)
        ] into (x, y)
        run (test "source parameterize test" {
          do! assertEquals x y
        })
      }
    test "parameterize tests should be able to run" {
      do! innerTests |> shouldEqualErrorCount 1
    }
