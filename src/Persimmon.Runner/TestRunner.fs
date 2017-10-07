module Persimmon.Runner.TestRunner

open Persimmon
open Persimmon.Internals

/// Run all tests synchronously.
/// TODO: Omit all synch caller.
//[<Obsolete>]
let runAllTests progress filter (tests: #TestMetadata seq) =
  let runner = TestRunner()
  let filter = TestFilter.make filter
  runner.RunSynchronouslyAllTests(progress, filter, tests)

/// Run all tests.
let asyncRunAllTests progress filter (tests: #TestMetadata seq) =
  let runner = TestRunner()
  let filter = TestFilter.make filter
  runner.AsyncRunAllTests(progress, filter, tests)
