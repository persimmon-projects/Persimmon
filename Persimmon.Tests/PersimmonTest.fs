namespace Persimmon.Tests

open Persimmon
open NUnit.Framework
open FsUnit

[<TestFixture>]
module PersimmonTest =

  let run (x: TestCase<_>) = x.Run()

  let shouldPassed<'T> (expected: 'T) = function
    | Done (_, (Persimmon.Passed (actual: 'T), [])) -> actual |> should equal expected
    | Done (_, results) -> Assert.Fail(sprintf "%A" (results |> NonEmptyList.toList))
    | Break (_, e, results) -> Assert.Fail(sprintf "%A\n%A" e results)

  let shouldNotPassed<'T> (expectedMessage: NonEmptyList<string>) = function
    | Done (_, (Persimmon.Passed (actual: 'T), [])) ->
        Assert.Fail(sprintf "Expect: Failure\nActual: %A" actual)
    | Done (_, results) ->
        results
        |> NonEmptyList.map (function NotPassed (Skipped x | Violated x) -> x | Persimmon.Passed x -> sprintf "Expected is NotPased but Passed(%A)" x)
        |> should equal expectedMessage
    | Break (_, e, results) -> Assert.Fail(sprintf "%A\n%A" e results)

  [<Test>]
  let ``simple succes assertion should succceed`` () =
    test "simple success assertion should succee" {
      return! pass 1
    }
    |> run
    |> shouldPassed 1

  [<Test>]
  let ``simple failure asseertion should fail`` () =
    let msg = "always fail"
    test "simple failure assertion should fail" {
      return! fail msg
    }
    |> run
    |> shouldNotPassed (msg, [])

  [<Test>]
  let ``all unit type assertion should run`` () =
    test "all unit type assertion should run" {
      do! assertEquals 1 2
      do! assertEquals 2 2
      do! assertEquals 3 4
    }
    |> run
    |> shouldNotPassed<unit> ("Expect: 1\nActual: 2", [ "Expect: 3\nActual: 4" ])

  let table = Map.ofList [("x", "y"); ("y", "z"); ("z", "other")]

  let test1 = test "success" {
    let value = table |> Map.find "x"
    do! assertEquals "y" value
    return value
  }

  let test2 = test "more success" {
    let! res = test1
    let value = table |> Map.find res
    do! assertEquals "z" value
    return value
  }

  let test3 = test "failure" {
    let! res = test2
    let value = table |> Map.find res
    do! assertEquals "x" value
    return value
  }

  let test4 = test "more failure" {
    let! res = test3
    let value = table |> Map.find res
    do! assertEquals "y" value // not execute
    return value
  }

  [<Test>]
  let ``test should be able to compose`` () =
    test1 |> run |> shouldPassed "y"
    test2 |> run |> shouldPassed "z"
    test3 |> run |> shouldNotPassed ("Expect: \"x\"\nActual: \"other\"", [])
    test4 |> run |> shouldNotPassed ("Expect: \"x\"\nActual: \"other\"", [])
