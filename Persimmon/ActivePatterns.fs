﻿module Persimmon.ActivePatterns

let (|Context|TestCase|TestCaseWithBeforeOrAfter|) (test: TestObject) =
  match test with
  | :? Context as ctx -> Context ctx
  | :? TestCaseWithBeforeOrAfter as tc -> TestCaseWithBeforeOrAfter tc.Body
  | tc -> TestCase (tc.GetType().GetMethod("BoxTypeParam").Invoke(tc, [||]) :?> TestCase<obj>)

let (|ContextResult|TestResult|EndMarker|) (res: ITestResult) =
  match res with
  | :? ContextResult as cr -> ContextResult cr
  | marker when marker = TestResult.endMarker -> EndMarker
  | tr -> TestResult (tr.GetType().GetMethod("BoxTypeParam").Invoke(tr, [||]) :?> TestResult<obj>)