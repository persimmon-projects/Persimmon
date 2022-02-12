module Persimmon.Console.Runner

open Persimmon
open Persimmon.Internals
open Persimmon.Output
open System
open System.Diagnostics
open System.IO

type TestManagerCallback(progress: TestResult -> unit) =
  inherit MarshalByRefObject()

  interface ITestManagerCallback with
    member this.Progress(testResult) = progress testResult

let collectAndRun (args: Args) (testManager: TestManager) (assemblyPath: string) =
  use progress = Args.progressPrinter args
  testManager.Callback <- TestManagerCallback(progress.Print)
  testManager.Parallel <- args.Parallel
  testManager.Filter <- args.Filter
  
  testManager.Collect(assemblyPath)
  testManager.Run()

let runAndReport (strategy: IRunnerStrategy) (args: Args) (watch: Stopwatch) (reporter: Reporter) (founds: FileInfo list)  =
  let mutable errors = 0
  let results = ResizeArray<_>()

  watch.Start()

  // collect and run
  for assembly in founds do
    let testResults = collectAndRun args (strategy.CreateTestManager(assembly)) assembly.FullName
    do
      errors <- errors + testResults.Errors
      results.AddRange(testResults.Results)

  watch.Stop()

  // report
  reporter.ReportProgress(TestResult.endMarker)
  reporter.ReportSummary(results)
  errors

let run (strategy: IRunnerStrategy) (args: Args) =
  let watch = Stopwatch()
  
  let requireFileName = Args.requireFileName args
  use reporter = Args.reporter watch args

  if args.Help then
    reporter.ReportError(Args.help)

  let founds, notFounds = args.Inputs |> List.partition (fun file -> file.Exists)
  if founds |> List.isEmpty then
    reporter.ReportError("input is empty.")
    -1
  elif requireFileName && Option.isNone args.Output then
    reporter.ReportError("xml format option require 'output' option.")
    -2
  elif notFounds |> List.isEmpty then
    try
      runAndReport strategy args  watch reporter founds
    with e ->
      reporter.ReportError("!!! FATAL Error !!!")
      reporter.ReportError(e.ToString())
      if e.InnerException <> null then
        reporter.ReportError("InnerException:")
        reporter.ReportError(e.InnerException.ToString())
      -100
  else
    reporter.ReportError("file not found: " + (String.Join(", ", notFounds)))
    -2