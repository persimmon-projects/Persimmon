[<AutoOpen>]
module Persimmon.Assertions

/// This assertion is always violated.
let fail message = NotPassed (Violated message)
/// This assertion is always passed.
let pass value = Passed value

let assertEquals expected actual =
  if expected = actual then pass ()
  else fail (sprintf "Expect: %A\nActual: %A" expected actual)

let assertNotEquals notExpected actual =
  if notExpected <> actual then pass ()
  else fail (sprintf "Not Expected: %A\nActual: %A" notExpected actual)

let assertPred pred =
  if pred then pass ()
  else fail "assert fail."

let ignoreResult message (_: AssertionResult<_>) = NotPassed (Skipped message)

let violatedMessage message = function
| NotPassed (Violated _) -> NotPassed (Violated message)
| other -> other

let replaceViolatedMessage replacer = function
| NotPassed (Violated msg) -> NotPassed (Violated (replacer msg))
| other -> other
