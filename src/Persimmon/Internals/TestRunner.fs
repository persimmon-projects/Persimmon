namespace Persimmon.Internals

open System
open System.Collections.Generic
open System.Reflection

open Microsoft.FSharp.Collections

open Persimmon
open Persimmon.ActivePatterns

module private TestRunnerImpl =

  let rec asyncRunTest (progress: TestResult -> unit) test = seq {
    match test with
    | Context context ->
      yield! context.Children |> Seq.collect (fun child ->
        asyncRunTest progress child)
    | TestCase testCase -> yield async {
        let! result = testCase.AsyncRun()
        do progress result
        return result
      }
  }

  let rec countErrors (testResult: #TestResult) =
    match testResult with
    | TestResult testResult ->
      let typicalRes = AssertionResult.Seq.typicalResult testResult.Results
      match typicalRes.Status with
      | Violated -> 1
      | _ -> 0
    | EndMarker -> 0

type RunResult = {
  Errors: int
  ExecutedRootTestResults: TestResult[]
}

[<Sealed>]
type TestRunner() =

  /// Collect test objects and run tests.
  member __.AsyncRunAllTests progress (tests: #TestMetadata seq) = async {
    let! results = tests |> Seq.collect (TestRunnerImpl.asyncRunTest progress) |> Async.Parallel
    let errors = results |> Seq.sumBy TestRunnerImpl.countErrors
    return { Errors = errors; ExecutedRootTestResults = results }
  }

  /// Collect test objects and run tests.
  /// TODO: Omit all synch caller.
  //[<Obsolete>]
  member this.RunAllTests progress (tests: #TestMetadata seq) =
    this.AsyncRunAllTests progress tests |> Async.RunSynchronously
      
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

    // Run tests.
    do testCases
      |> Seq.filter (fun testCase -> containsKey testCase.UniqueName)
      |> Seq.iter (fun testCase -> TestRunnerImpl.asyncRunTest callback.Invoke testCase |> ignore)
