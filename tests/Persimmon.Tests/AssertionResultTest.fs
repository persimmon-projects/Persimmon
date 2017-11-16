namespace Persimmon.Tests

open Persimmon
open UseTestNameByReflection

module AssertionResultTest =
  open Persimmon.TestResult

  let ``should equal`` = parameterize {
    source [
      (Passed "a"), (Passed "a"), true
      (Passed "a"), (Passed "b"), false

      (Passed null), (Passed null), true
      (Passed "a"), (Passed null), false

      (NotPassed (Some 0, Skipped "skip")), (NotPassed (Some 0, Skipped "skip")), true
      (NotPassed (Some 0, Skipped "skip")), (NotPassed (Some 1, Skipped "skip")), false
      (NotPassed (Some 0, Skipped "skip")), (NotPassed (Some 0, Skipped "SKIP")), false

      (Passed "a"), (NotPassed (Some 0, Skipped "skip")), false
    ]
    run (fun (left, right, expected) -> test {
      do! (left = right) |> assertEquals expected
    })
  }
