module Persimmon.Runner.TestRunner

open Persimmon
open Persimmon.Internals

let runAllTests progress (tests: #TestMetadata seq) =
  let runner = new TestRunner()
  runner.RunAllTests progress tests

let asyncRunAllTests progress (tests: #TestMetadata seq) =
  let runner = new TestRunner()
  runner.AsyncRunAllTests progress tests
