module Persimmon.Tests.Internals.RunnerTest

open Persimmon
open UseTestNameByReflection
open Persimmon.Tests

let ``count errors`` = parameterize {
  source [
    (TestCase.makeDone None Seq.empty (Passed ()), 0)
    (TestCase.makeDone None Seq.empty (NotPassed (Violated "")), 1)
    (TestCase.makeError None Seq.empty (exn()), 1)
    (TestCase.makeError None Seq.empty (exn()) |> TestCase.addNotPassed (Violated ""), 1)
  ]
  run (fun (case, expected) -> test {
    do!
      case
      |> Helper.run
      |> Persimmon.Internals.TestRunnerImpl.countErrors
      |> assertEquals expected
  })
}
