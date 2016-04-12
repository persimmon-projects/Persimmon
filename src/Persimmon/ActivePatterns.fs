module Persimmon.ActivePatterns

open System

let (|Context|TestCase|) (testMetadata: ITestMetadata) =
  match testMetadata with
  | :? Context as context -> Context context
  | :? ITestCase as testCase -> TestCase testCase
  | _ -> new InvalidOperationException() |> raise

let (|TestResult|EndMarker|) (result: #ITestResult) =
  match result :> ITestResult with
  | marker when marker = TestResult.endMarker -> EndMarker
  | _ -> TestResult result
  