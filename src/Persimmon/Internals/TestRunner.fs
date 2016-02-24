namespace Persimmon.Internals

open Microsoft.FSharp.Collections

open Persimmon
open Persimmon.ActivePatterns
open Persimmon.Output

module private TestRunnerImpl =

  let runTests (reporter: Reporter) (test: ITestObject) =
    match test with
    | Context ctx -> ctx.Run(reporter.ReportProgress) :> ITestResult
    | TestCase tc ->
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

type RunResult = {
  Errors: int
  ExecutedRootTestResults: ITestResult seq
}

[<Sealed>]
type TestRunner() =
  
  member __.RunAllTests reporter (tests: #ITestObject seq) =
    let rootResults = tests |> Seq.map (TestRunnerImpl.runTests reporter)
    let errors = rootResults |> Seq.sumBy TestRunnerImpl.countErrors
    { Errors = errors; ExecutedRootTestResults = rootResults }
    
  member __.AsyncRunAllTests reporter (tests: #ITestObject seq) =
    let asyncRun test = async {
        return TestRunnerImpl.runTests reporter test
    }
    async {
        let! rootResults = tests |> Seq.map asyncRun |> Async.Parallel
        let errors = Seq.ofArray rootResults |> Seq.sumBy TestRunnerImpl.countErrors
        return { Errors = errors; ExecutedRootTestResults = rootResults }
    }

#if NET4
  // TODO: CLR4's Task<'T>
#endif
