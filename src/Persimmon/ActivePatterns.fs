module Persimmon.ActivePatterns

open System

/// Retreive Context/TestCase from TestMetadata.
let (|Context|TestCase|) (testMetadata: TestMetadata) =
  match testMetadata with
  | :? Context as context -> Context context
  | :? TestCase as testCase -> TestCase testCase
  | _ -> new InvalidOperationException() |> raise

/// Retreive ContextResult/TestResult/EndMarker from test result.
let (|ContextResult|TestResult|EndMarker|) (result: ResultNode) =
  match result with
  | :? TestResult as testResult ->
    match testResult with
    | _ when testResult = TestResult.endMarker -> EndMarker
    | _ -> TestResult testResult
  | :? ContextResult as contextResult -> ContextResult contextResult
  | _ -> new ArgumentException() |> raise
  