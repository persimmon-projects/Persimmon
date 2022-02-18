module Persimmon.Console.RunnerStrategy

open Persimmon.Internals
open Persimmon
open System
open System.Reflection
open System.Diagnostics
open Persimmon.Output

type ITestContext =
  abstract Callback: ITestManagerCallback with set
  abstract Parallel: bool with set
  abstract Filter: TestFilter with set
  abstract Collect: unit -> unit
  abstract Run: unit -> RunResult<ResultNode>

type IRunnerStrategy =
  abstract CreateTestContext: unit -> ITestContext seq

type TestManagerCallback(progress: TestResult -> unit) =
  inherit MarshalByRefObject()

  interface ITestManagerCallback with
    member this.Progress(testResult) = progress testResult

[<AbstractClass>]
type RunnerStrategyBase() =
  let manager = TestManager()
  abstract CollectTests: unit -> seq<TestMetadata>

  interface IRunnerStrategy with
    member this.CreateTestContext(): seq<ITestContext> =
      { new ITestContext with
          member _.Callback with set(value) = manager.Callback <- value
          member _.Filter with set(value) = manager.Filter <- value
          member _.Parallel with set(value) = manager.Parallel <- value
          member _.Collect() = manager.AddTests(this.CollectTests())
          member _.Run() = manager.Run()
      }
      |> Seq.singleton


type LoadFromAssemblyStrategy(assemblies: seq<Assembly>) =
  inherit RunnerStrategyBase()

  override _.CollectTests() =
    [|
      for asm in assemblies do
        yield! TestCollector().Collect(asm)
    |]

type InstanceStrategy(tests: seq<TestMetadata>) =
  inherit RunnerStrategyBase()
  
    override _.CollectTests() = tests

let collectAndRun (args: Args) (context: ITestContext) =
  use progress = Args.progressPrinter args
  context.Callback <- TestManagerCallback(progress.Print)
  context.Parallel <- args.Parallel
  context.Filter <- args.Filter
  
  context.Collect()
  context.Run()

let runAndReport (strategy: IRunnerStrategy) (args: Args) (watch: Stopwatch) (reporter: Reporter) =
  let mutable errors = 0
  let results = ResizeArray<_>()

  watch.Start()

  // collect and run
  for context in strategy.CreateTestContext() do
    let testResults = collectAndRun args context
    do
      errors <- errors + testResults.Errors
      results.AddRange(testResults.Results)

  watch.Stop()

  // report
  reporter.ReportProgress(TestResult.endMarker)
  reporter.ReportSummary(results)
  errors