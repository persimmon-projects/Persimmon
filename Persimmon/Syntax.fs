[<AutoOpen>]
module Persimmon.Syntax

/// Create the context.
let context name children = Context(name, children)

/// Create the test case.
let test name = TestBuilder(name)
/// Create the parameterized test case.
let parameterize = ParameterizeBuilder()

/// Skip the test case.
let skip message (target: TestCase<'T>) : TestCase<'T> =
  TestCase.make target.Name target.Parameters (NotPassed (Skipped message))

/// Trap the exception and convert to AssertionResult<exn>.
let trap = TrapBuilder()
