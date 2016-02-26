module Persimmon.Runner.TestRunner

open Persimmon
open Persimmon.Internals

let runAllTests progress (tests: #ITestObject seq) =
  let runner = new TestRunner()
  runner.RunAllTests progress tests

let asyncRunAllTests progress (tests: #ITestObject seq) =
  let runner = new TestRunner()
  runner.AsyncRunAllTests progress tests
