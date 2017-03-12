module Persimmon.Tests.AssertionTest

open Persimmon
open UseTestNameByReflection

let ``get line number`` =
  test {
    match Assert.Fail("test") with
    | NotPassed(line, _) ->
      do! line |> assertEquals (Some 8)
    | Passed _ ->
      do! fail "expected NotPassed, but was Passed"
  }
