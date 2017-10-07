namespace Persimmon.Internals

open System
open System.Collections.Generic
open System.Reflection

open Microsoft.FSharp.Collections

open Persimmon
open Persimmon.ActivePatterns

module internal TestRunnerImpl =

  let rec collectTests progress (results: ResizeArray<ResultNode>) (filter: TestMetadata -> bool) (test: TestMetadata) : seq<Async<unit -> unit>> = seq {
    if filter test then
      match test with
      | Context context ->
        let childResults = ResizeArray<ResultNode>()
        yield! Seq.collect (collectTests progress childResults filter) context.Children

        let delayMakeResult = fun () -> results.Add(ContextResult(context, childResults.ToArray()))
        yield async { return delayMakeResult }
      | TestCase testCase ->
        let delayMakeResult = fun testCaseResult () -> results.Add(testCaseResult)
        yield async {
          let! testCaseResult = testCase.AsyncRun()
          do progress testCaseResult
          return delayMakeResult testCaseResult
        }
    else
      ()
  }

  let runTests progress eval filter (tests: seq<#TestMetadata>) =
    let results = ResizeArray<ResultNode>()

    async {
      let! delayMakeResults = tests |> Seq.collect (collectTests progress results filter) |> eval

      do delayMakeResults |> Seq.iter (fun f -> f())
      return results.ToArray()
    }

  let asyncSequential xs = async {
    let list = ResizeArray<'T>()
    for asy in xs do
      let! v = asy
      list.Add(v)
    return list.ToArray()
  }

  let rec countErrors result =
    match result with
    | ContextResult contextResult ->
      contextResult.Results |> Seq.sumBy countErrors
    | TestResult testResult ->
      match testResult.Box() with
      | Error _ -> 1
      | Done _ ->
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
  member __.AsyncRunAllTests(progress, filter, tests) = async {
    let! testResults = TestRunnerImpl.runTests progress Async.Parallel filter tests
    let errors = testResults |> Seq.sumBy TestRunnerImpl.countErrors
    return { Errors = errors; Results = testResults }
  }

  /// Collect test objects and run tests.
  member __.AsyncRunSynchronouslyAllTests(progress, filter, tests) = async {
    let! testResults = TestRunnerImpl.runTests progress TestRunnerImpl.asyncSequential filter tests
    let errors = testResults |> Seq.sumBy TestRunnerImpl.countErrors
    return { Errors = errors; Results = testResults }
  }

  /// Collect test objects and run tests.
  /// TODO: Omit all synch caller.
  //[<Obsolete>]
  member __.RunSynchronouslyAllTests(progress, filter, tests) =
    // Keep forward sequence.
    let testResults = TestRunnerImpl.runTests progress TestRunnerImpl.asyncSequential filter tests |> Async.RunSynchronously
    let errors = testResults |> Seq.sumBy TestRunnerImpl.countErrors
    { Errors = errors; Results = testResults }

  /// RunTestsAndCallback run test cases and callback. (Internal use only)
  /// If fullyQualifiedTestNames is empty, try all tests.
  member __.RunTestsAndCallback(target: Assembly, fullyQualifiedTestNames: string[], before: Action<obj>, callback: Action<obj>) =

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
    |> Seq.choose (fun testCase ->
      // Include only fqtn
      if containsKey testCase.UniqueName then
        before.Invoke(testCase)
        // Map test case to async runner (with callback side effect)
        let passAll = fun _ -> true
        Some (TestRunnerImpl.runTests callback.Invoke TestRunnerImpl.asyncSequential passAll [ testCase ])
      else None
    )
    |> Async.Parallel
    |> Async.RunSynchronously |> ignore

  member this.RunTestsAndCallback(target: Assembly, fullyQualifiedTestNames: string[], callback: Action<obj>) =
    let before = Action<obj>(ignore)
    this.RunTestsAndCallback(target, fullyQualifiedTestNames, before, callback)
