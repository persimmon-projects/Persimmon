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

/// Retreive information from non generic AssertionResult.
let (|Passed|NotPassed|) (ar: AssertionResult) =
  match ar.Status with
  | None -> Passed
  | Some cause -> NotPassed cause

/// Retreive information from non generic TestResult.
let (|Done|Error|) (result: TestResult) =
  match result.IsError with
  | true ->
    Error (
      result.TestCase,
      result.Exceptions,
      result.AssertionResults |> Seq.choose (function NotPassed cause -> Some cause | _ -> None) |> Seq.toArray,
      result.Duration)
  | false ->
    Done (result.TestCase, result.AssertionResults, result.Duration)
