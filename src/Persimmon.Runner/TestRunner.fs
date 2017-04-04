module Persimmon.Runner.TestRunner

open Persimmon
open Persimmon.Internals

/// Run all tests synchronously.
/// TODO: Omit all synch caller.
//[<Obsolete>]
let runAllTests progress (tests: #TestMetadata seq) =
  let runner = TestRunner()
  runner.RunSynchronouslyAllTests(progress, tests)

/// Run all tests.
let asyncRunAllTests progress (tests: #TestMetadata seq) =
  let runner = TestRunner()
  runner.AsyncRunSynchronouslyAllTests(progress, tests)
