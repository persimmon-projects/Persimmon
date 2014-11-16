[<AutoOpen>]
module Persimmon.Syntax

/// Create the context.
let context name children = Context(name, children)

/// Create the test case.
let test name = TestBuilder(name)
/// Create the parameterized test case.
let parameterize = ParameterizeBuilder()

/// Trap the exception and convert to AssertionResult<exn>.
let trap = TrapBuilder()
