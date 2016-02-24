module Persimmon.Runner.TestRunner

open Persimmon
open Persimmon.Internals

let runAllTests reporter (tests: #ITestObject seq) =
  let runner = new TestRunner()
  runner.RunAllTests reporter tests

let asyncRunAllTests reporter (tests: #ITestObject seq) =
  let runner = new TestRunner()
  runner.AsyncRunAllTests reporter tests
