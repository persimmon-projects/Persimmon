namespace Persimmon.Tests

open Persimmon
open NUnit.Framework
open FsUnit

[<TestFixture>]
module PersimmonTest =

  let shouldSucceed<'T> (expected: 'T) = function
    | TestResult { AssertionResult = Passed actual } -> actual |> should equal expected
    | TestResult { AssertionResult = Failed xs } -> Assert.Fail(sprintf "%A" xs)
    | TestResult { AssertionResult = Error (e, xs) } -> Assert.Fail(sprintf "%A\n%A" e xs)

  let shouldFail<'T> (expectedMessage: NonEmptyList<string>) = function
    | TestResult { AssertionResult = Passed x } -> Assert.Fail(sprintf "Expect: Failure\nActual: %A" x)
    | TestResult { AssertionResult = Failed actual } -> actual |> should equal expectedMessage
    | TestResult { AssertionResult = Error (e, xs) } -> Assert.Fail(sprintf "%A\n%A" e xs)

  [<Test>]
  let ``simple succes assertion should succceed`` () =
    test "simple success assertion should succee" {
      return! pass 1
    }
    |> shouldSucceed 1

  [<Test>]
  let ``simple failure asseertion should fail`` () =
    let msg = "always fail"
    test "simple failure assertion should fail" {
      return! fail msg
    }
    |> shouldFail (msg, [])

  [<Test>]
  let ``all unit type assertion should run`` () =
    test "all unit type assertion should run" {
      do! assertEquals 1 2
      do! assertEquals 2 2
      do! assertEquals 3 4
    }
    |> shouldFail<obj> ("Expect: 1\nActual: 2", [ "Expect: 3\nActual: 4" ])

  [<Test>]
  let ``dependent assertion should not run if before assertion failed`` () =
    test "dependent assertion should not run if before assertion failed" {
      let! v = check 1 2
      do! assertEquals v 3
    }
    |> shouldFail ("Expect: 1\nActual: 2", [])

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
    test1 |> shouldSucceed "y"
    test2 |> shouldSucceed "z"
    test3 |> shouldFail ("Expect: \"x\"\nActual: \"other\"", [])
    test4 |> shouldFail ("Expect: \"x\"\nActual: \"other\"", [])
