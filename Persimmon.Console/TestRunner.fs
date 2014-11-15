module TestRunner

open System
open System.IO
open System.Reflection

open Microsoft.FSharp.Reflection

open Persimmon
open Persimmon.ActivePatterns
open Persimmon.Output
open RuntimeUtil

type Result = {
  Errors: int
  ExecutedRootTestResults: ITestResult seq
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Result =
  let empty = { Errors = 0; ExecutedRootTestResults = Seq.empty }

let runTests (reporter: Reporter) (test: TestObject) =
  match test with
  | Context ctx -> ctx.Run(reporter.ReportProgress) :> ITestResult
  | TestCase tc ->
      let result = tc.Run()
      reporter.ReportProgress(result)
      result :> ITestResult

let runAllTests reporter (tests: TestObject list) =
  let rootResults = tests |> List.map (runTests reporter)
  // todo : aggregate the count of error tests
  { Errors = 0; ExecutedRootTestResults = rootResults }
