[<AutoOpen>]
module Persimmon.Syntax

open System.Diagnostics

/// Create the context.
let context name children = Context(name, children)

/// Create the test case.
let test (name: string) = TestBuilder(name)
/// Create the parameterized test case.
let parameterize = ParameterizeBuilder()

/// Skip the test case.
let skip message (target: TestCase<'T>) : TestCase<'T> =
  TestCase.make target.Name target.Parameters (NotPassed (Skipped message))

let timeout time (target: TestCase<'T>): TestCase<'T> =
  let body () =
    let watch = Stopwatch.StartNew()
    try
      Async.RunSynchronously(async { return target.Run() }, time)
    with
    | :? System.TimeoutException as e ->
      watch.Stop()
      Error(target.Metadata, [e], [], watch.Elapsed)
  TestCase<'T>(target.Metadata, body)

/// Trap the exception and convert to AssertionResult<exn>.
let trap = TrapBuilder()

let asyncRun = AsyncRunBuilder()

/// Create the test case from assertions.
let assertions = TestBuilder()

module UseTestNameByReflection =
  let test = TestBuilder()
