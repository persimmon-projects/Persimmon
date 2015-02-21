﻿[<AutoOpen>]
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

let asyncRun = AsyncRunBuilder()

let testWithBefore name before = TestWithBeforeOrAfterBuilder(name, Some before, None)
let testWithAfter name after = TestWithBeforeOrAfterBuilder(name, None, Some after)
let testWithBeforeAfter name before after = TestWithBeforeOrAfterBuilder(name, Some before, Some after)

module UseTestNameByReflection =
  let test = TestBuilder("")
  let testWithBefore before = TestWithBeforeOrAfterBuilder("", Some before, None)
  let testWithAfter after = TestWithBeforeOrAfterBuilder("", None, Some after)
  let testWithBeforeAfter before after = TestWithBeforeOrAfterBuilder("", Some before, Some after)
