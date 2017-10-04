module Persimmon.Tests.Internals.RunnerTest

open Persimmon
open UseTestNameByReflection
open Persimmon.Tests

let ``count errors`` = parameterize {
  source [
    (TestCase.makeDone None Seq.empty Seq.empty (Passed ()), 0)
    (TestCase.makeDone None Seq.empty Seq.empty (NotPassed(None, Violated "")), 1)
    (TestCase.makeError None Seq.empty Seq.empty (exn()), 1)
    (TestCase.makeError None Seq.empty Seq.empty (exn()) |> TestCase.addNotPassed None (Violated ""), 1)
  ]
  run (fun (case, expected) -> test {
    do!
      case
      |> Helper.run
      |> Persimmon.Internals.TestRunnerImpl.countErrors
      |> assertEquals expected
  })
}
