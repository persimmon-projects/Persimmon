namespace Persimmon.Internals

open System
open System.Collections.Generic
open System.Reflection

open Microsoft.FSharp.Collections

open Persimmon
open Persimmon.ActivePatterns

module private TestRunnerImpl =

  let rec traverseAsyncRunner test = seq {
    match test with
    | Context context ->
      yield! context.Children |> Seq.collect traverseAsyncRunner
    | TestCase testCase ->
      yield testCase.AsyncRun
   }
  /// Entire full parallelism test execution.
  and asyncRunTest progress test =
    traverseAsyncRunner test |> Seq.map (fun asyncRun -> async {
      let! result = asyncRun()
      do progress result
      return result
    }) |> Async.Parallel

  let asyncSequential xs = async {
    let list = new System.Collections.Generic.List<'T>()
    for asy in xs do
      let! v = asy
      list.Add(v)
    return list.ToArray()
  }

  // Oh... very hard...
  let rec asyncRunSynchronouslyTest progress test =
    match test with
    | Context context ->
      context.Children |> Seq.map (fun child -> async {
        let! results = asyncRunSynchronouslyTest progress child
        return new ContextResult(context, results) :> ResultNode
      }) |> asyncSequential
    | TestCase testCase -> async {
        let! result = testCase.AsyncRun()
        do progress result
        return [| result :> ResultNode |]
      }

  let rec countErrors result =
    match result with
    | ContextResult contextResult ->
      contextResult.Results |> Seq.sumBy countErrors
    | TestResult testResult ->
      let ar = AssertionResult.Seq.typicalResult testResult.AssertionResults
      match ar with
      | NotPassed (Violated _) -> 1
      | _ -> 0
    | EndMarker -> 0

/// Test results.
type RunResult<'T> = {
  Errors: int
  Results: 'T[]
}

[<Sealed>]
type TestRunner() =

  /// Collect test objects and run tests.
  member __.AsyncRunAllTests progress tests = async {
    let! testResultsList = tests |> Seq.map (TestRunnerImpl.asyncRunTest progress) |> TestRunnerImpl.asyncSequential
    let testResults = testResultsList |> Seq.collect (fun tr -> tr) |> Seq.toArray
    let errors = testResults |> Seq.sumBy TestRunnerImpl.countErrors
    return { Errors = errors; Results = testResults }
  }

  /// Collect test objects and run tests.
  member __.AsyncRunSynchronouslyAllTests progress tests = async {
    let! testResultsList = tests |> Seq.map (TestRunnerImpl.asyncRunSynchronouslyTest progress) |> TestRunnerImpl.asyncSequential
    let testResults = testResultsList |> Seq.collect (fun tr -> tr) |> Seq.toArray
    let errors = testResults |> Seq.sumBy TestRunnerImpl.countErrors
    return { Errors = errors; Results = testResults }
  }

  /// Collect test objects and run tests.
  /// TODO: Omit all synch caller.
  //[<Obsolete>]
  member __.RunSynchronouslyAllTests progress tests =
    // Keep forward sequence.
    let testResultsList = tests |> Seq.map (TestRunnerImpl.asyncRunSynchronouslyTest progress) |> Seq.map Async.RunSynchronously
    let testResults = testResultsList |> Seq.collect (fun tr -> tr) |> Seq.toArray
    let errors = testResults |> Seq.sumBy TestRunnerImpl.countErrors
    { Errors = errors; Results = testResults }
      
  /// RunTestsAndCallback run test cases and callback. (Internal use only)
  /// If fullyQualifiedTestNames is empty, try all tests.
  member __.RunTestsAndCallback (target: Assembly, fullyQualifiedTestNames: string[], callback: Action<obj>) =

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

    // Run tests with entire full parallelism.
    testCases
      // Include only fqtn
      |> Seq.filter (fun testCase -> containsKey testCase.UniqueName)
      // Map test case to async runner (with callback side effect)
      |> Seq.map (fun testCase -> TestRunnerImpl.asyncRunTest callback.Invoke testCase)
      // Full parallelism
      |> Async.Parallel
      // Synchronous execution
      |> Async.RunSynchronously |> ignore
