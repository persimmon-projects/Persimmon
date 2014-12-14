module Persimmon.Runner.TestRunner

open Persimmon
open Persimmon.ActivePatterns
open Persimmon.Output

type RunResult = {
  Errors: int
  ExecutedRootTestResults: ITestResult seq
}

let runTests (reporter: Reporter) (test: TestObject) =
  match test with
  | Context ctx -> ctx.Run(reporter.ReportProgress) :> ITestResult
  | TestCase tc
  | TestCaseWithBeforeOrAfter tc -> 
      let result = tc.Run()
      reporter.ReportProgress(result)
      result :> ITestResult

let rec countErrors = function
| ContextResult cr ->
    cr.Children |> List.sumBy countErrors
| TestResult tr ->
    match tr with
    | Error _ -> 1
    | Done (_, res, _) ->
        let typicalRes = AssertionResult.NonEmptyList.typicalResult res
        match typicalRes with
        | NotPassed (Violated _) -> 1
        | NotPassed (Skipped _) -> 0
        | Passed _ -> 0
| EndMarker -> 0

let runAllTests reporter (tests: TestObject list) =
  let rootResults = tests |> List.map (runTests reporter)
  let errors = rootResults |> List.sumBy countErrors
  { Errors = errors; ExecutedRootTestResults = rootResults }
