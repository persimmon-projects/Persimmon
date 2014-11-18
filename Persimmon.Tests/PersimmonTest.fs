namespace Persimmon.Tests

open Persimmon

module PersimmonTest =

  let run (x: TestCase<_>) = x.Run()

  let shouldPassed<'T when 'T : equality> (expected: 'T) (x: TestCase<'T>) =
    let inner = function
    | Done (m, (Persimmon.Passed (actual: 'T), [])) -> Done (m, (assertEquals expected actual, []))
    | Done (m, results) -> Done (m, results |> NonEmptyList.map (function
      | Passed _ -> Passed ()
      | NotPassed x -> NotPassed x))
    | Error (m, es, results) -> Error (m, es, results)
    TestCase({ Name = x.Name; Parameters = x.Parameters }, fun () -> inner (run x))

  let shouldNotPassed<'T> (expectedMessages: NonEmptyList<string>) (x: TestCase<'T>) =
    let inner = function
    | Done (m, (Persimmon.Passed (actual: 'T), [])) ->
      Done (m, (fail (sprintf "Expect: Failure\nActual: %A" actual), []))
    | Done (m, results) ->
        results
        |> NonEmptyList.map (function NotPassed (Skipped x | Violated x) -> x | Persimmon.Passed x -> sprintf "Expected is NotPased but Passed(%A)" x)
        |> fun actual -> Done (m, (assertEquals expectedMessages actual, []))
    | Error (m, es, results) -> Error (m, es, results)
    TestCase({ Name = x.Name; Parameters = x.Parameters }, fun () -> inner (run x))

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
