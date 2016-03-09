namespace Persimmon.Internals

open System
open System.Collections.Generic
open System.Reflection

open Microsoft.FSharp.Collections

open Persimmon
open Persimmon.ActivePatterns
open Persimmon.Output

module private TestRunnerImpl =

  let runTest progress test =
    match test with
    | Context ctx -> ctx.Run(progress) :> ITestResultNode
    | TestCase tc ->
      let result = tc.Run()
      progress result
      result :> ITestResultNode

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
  ExecutedRootTestResults: ITestResultNode seq
}

[<Sealed>]
type TestRunner() =
  
  member __.RunAllTests progress (tests: #ITestObject seq) =
    let rootResults = tests |> Seq.map (TestRunnerImpl.runTest progress)
    let errors = rootResults |> Seq.sumBy TestRunnerImpl.countErrors
    { Errors = errors; ExecutedRootTestResults = rootResults }
    
  member __.AsyncRunAllTests progress (tests: #ITestObject seq) =
    let asyncRun test = async {
        return TestRunnerImpl.runTest progress test
    }
    async {
        let! rootResults = tests |> Seq.map asyncRun |> Async.Parallel
        let errors = Seq.ofArray rootResults |> Seq.sumBy TestRunnerImpl.countErrors
        return { Errors = errors; ExecutedRootTestResults = rootResults }
    }
      
  /// RunTestsAndCallback is safe-serializable-types runner method.
  member __.RunTestsAndCallback (target: Assembly, fullyQualifiedTestNames: string[], callback: Action<obj>) =
    let progress (testResult: ITestResultNode) =
      match testResult with
      // Call f if testResult is ITestResult (ignoring ContextResult)
      | :? ITestResult as tr -> callback.Invoke(tr)
      | _ -> ()

    // Make fqtn dicts.
    let targetNames = Dictionary<string, string>()
    for name in fullyQualifiedTestNames do targetNames.Add(name, name)

    // If fqtn is empty, try all tests.
    let containsKey key =
      match targetNames.Count with
      | 0 -> true
      | _ -> targetNames.ContainsKey key

    // Collect test cases.
    let collector = TestCollector()
    let testCases = collector.CollectOnlyTestCases(target)

    // Run tests.
    do testCases
      |> Seq.filter (fun testCase -> containsKey testCase.FullName)
      |> Seq.iter (fun testCase -> TestRunnerImpl.runTest progress testCase |> ignore)
