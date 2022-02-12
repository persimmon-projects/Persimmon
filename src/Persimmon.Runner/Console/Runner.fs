module Persimmon.Console.Runner

open Persimmon
open System
open System.Diagnostics
open Persimmon.Console.RunnerStrategy
open System.Reflection

let run (args: Args) (strategy: IRunnerStrategy) =
  let watch = Stopwatch()
  
  let requireFileName = Args.requireFileName args
  use reporter = Args.reporter watch args

  if args.Help then
    reporter.ReportError(Args.help)

  let _, notFounds = args.Inputs |> List.partition (fun file -> file.Exists)
  if requireFileName && Option.isNone args.Output then
    reporter.ReportError("xml format option require 'output' option.")
    -2
  elif notFounds |> List.isEmpty then
    try
      runAndReport strategy args  watch reporter
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

let runTestsInAssembly (args: Args) (asm: Assembly) =
  run args (LoadFromAssemblyStrategy(asm))

let runTests (args: Args) (tests: seq<#TestMetadata>) =
  run args (InstanceStrategy(tests |> Seq.cast<TestMetadata>))