module Persimmon.ActivePatterns

let (|ContextResult|TestResult|) (res: ITestResult) =
  match res with
  | :? ContextResult as cr -> ContextResult cr
  | tr -> TestResult (tr.GetType().GetMethod("BoxTypeParam").Invoke(tr, [||]) :?> TestResult<obj>)