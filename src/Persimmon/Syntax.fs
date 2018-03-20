[<AutoOpen>]
module Persimmon.Syntax

open System.Diagnostics

/// Create the context.
let context name children = Context(name, [], children)

/// Create the test case.
let test (name: string) = TestBuilder(name)
/// Create the parameterized test case.
let parameterize = ParameterizeBuilder()

/// Skip the test case.
let skip message (target: TestCase<'T>) : TestCase<'T> =
  TestCase.makeDone target.Name target.Categories target.Parameters (NotPassed(None, Skipped message))

let timeout time (target: TestCase<'T>): TestCase<'T> =
  let body _ = async {
    let watch = Stopwatch.StartNew()
    return
      try
        Async.RunSynchronously(target.AsyncRun(), time)
      with
      | :? System.TimeoutException as e ->
        watch.Stop()
        Error(target, [| ExceptionWrapper(e) |], [], watch.Elapsed)
  }
  TestCase<'T>(target.Name, target.Categories, target.Parameters, body)

/// Add the categories to the test case
let categories categories (target: TestCase<'T>) : TestCase<'T> =
  let newCategories = Seq.append target.Categories categories
  let body = fun _ -> target.AsyncRun()
  TestCase<'T>(target.Name, newCategories, target.Parameters, body)

/// Add the category to the test case
let category category (target: TestCase<'T>) : TestCase<'T> = categories [ category ] target

/// Trap the exception and convert to AssertionResult<exn>.
let trap = TrapBuilder()

let asyncRun = AsyncRunBuilder()

/// Create the test case from assertions.
let assertions = TestBuilder()

module UseTestNameByReflection =
  let test = TestBuilder()
