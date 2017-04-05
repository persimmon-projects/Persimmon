namespace Persimmon

#if NET45 || NETSTANDARD
open System.Runtime.CompilerServices
#else
open System

[<AttributeUsage(AttributeTargets.Parameter, Inherited = false); Sealed>]
type CallerLineNumberAttribute() =
  inherit Attribute()
#endif

type Assert =

  static member Pass(value) = Passed value

  static member Ignore(message, [<CallerLineNumber>]?line : int) =
    NotPassed(line, Skipped message)

  static member Fail(message, [<CallerLineNumber>]?line : int) =
    NotPassed(line, Violated message)

  static member Equal(expected, actual, [<CallerLineNumber>]?line : int) =
    if expected = actual then Assert.Pass()
    else
      let message = sprintf "Expect: %A\nActual: %A" expected actual
      Assert.Fail(message, ?line = line)

  static member NotEqual(expected, actual, [<CallerLineNumber>]?line : int) =
    if expected <> actual then Assert.Pass()
    else
      let message = sprintf "Not Expect: %A\nActual: %A" expected actual
      Assert.Fail(message, ?line = line)

  static member Pred(pred, [<CallerLineNumber>]?line : int) =
    if pred then Assert.Pass()
    else Assert.Fail("Assertion failed.", ?line = line)

[<AutoOpen>]
module Assertions =

  /// This assertion is always violated.
  let inline fail message = Assert.Fail(message, ?line = None)

  /// This assertion is always passed.
  let inline pass value = Assert.Pass(value)

  let inline assertEquals expected actual =
    Assert.Equal(expected, actual, ?line = None)

  let inline assertNotEquals notExpected actual =
    Assert.NotEqual(notExpected, actual, ?line = None)

  let inline assertPred pred =
    Assert.Pred(pred, ?line = None)

  let inline ignoreResult message (_: AssertionResult<_>) =
    Assert.Ignore(message, ?line = None)

  let violatedMessage message = function
  | NotPassed(line, Violated _) -> NotPassed(line, Violated message)
  | other -> other

  let replaceViolatedMessage replacer = function
  | NotPassed(line, Violated msg) -> NotPassed(line, Violated (replacer msg))
  | other -> other
