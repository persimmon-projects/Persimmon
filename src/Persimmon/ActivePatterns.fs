module Persimmon.ActivePatterns

open System

let (|Context|TestCase|) (testMetadata: TestMetadata) =
  match testMetadata with
  | :? Context as context -> Context context
  | :? TestCase as testCase -> TestCase testCase
  | _ -> new InvalidOperationException() |> raise

let (|TestResult|EndMarker|) (result: #TestResult) =
  match result :> TestResult with
  | marker when marker = TestResult.endMarker -> EndMarker
  | _ -> TestResult result
  