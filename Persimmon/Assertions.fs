[<AutoOpen>]
module Persimmon.Assertions

let fail message = NotPassed (Violated message)
let pass value = Passed value

let check expected actual =
  if expected = actual then pass actual
  else fail (sprintf "Expect: %A\nActual: %A" expected actual)

let assertEquals expected actual =
  if expected = actual then pass ()
  else fail (sprintf "Expect: %A\nActual: %A" expected actual)

let assertNotEquals notExpected actual =
  if notExpected <> actual then pass ()
  else fail (sprintf "Not Expected: %A\nActual: %A" notExpected actual)

let assertPred pred =
  if pred then pass ()
  else fail "assert fail."