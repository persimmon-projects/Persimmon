module Persimmon.Tests.AssertionTest

open Persimmon
open UseTestNameByReflection

let ``get line number`` =
  let curryStyle = match test.Source(fail "test") with | UnitAssertionResult x -> x | _ -> failwith "expected AssertionResult<unit>"
  parameterize {
    source [
      (Assert.Fail("test"), 10)
      (curryStyle, 7)
    ]
    run (fun (assertion, n) -> test {
      match assertion with
      | NotPassed(line, _) ->
        do! line |> assertEquals (Some n)
      | Passed _ ->
        do! fail "expected NotPassed, but was Passed"
    })
  }
