module Persimmon

type ReturnType = UnitType | ValueType

type AssertionResult<'T> =
  | Success of 'T
  | Failure of NonEmptyList<string>

module AssertionResult =
  let map f = function
  | Success s -> Success (f s)
  | Failure errs -> Failure errs

type TestResult<'T> = {
  Name: string
  AssertionResult: AssertionResult<'T>
}

module TestResult =
  let map f x =
    { Name = x.Name; AssertionResult = AssertionResult.map f x.AssertionResult }

type TestBuilder(description: string) =
  member __.Return(x) = Success x
  member __.ReturnFrom(x, _) = x
  member __.Source(x: AssertionResult<unit>) = (x, UnitType)
  member __.Source(x: AssertionResult<_>) = (x, ValueType)
  member __.Source(x: TestResult<unit>) = (x.AssertionResult, UnitType)
  member __.Source(x: TestResult<_>) = (x.AssertionResult, ValueType)
  member __.Bind(x, f: 'T -> AssertionResult<_>) =
    match x with
    | (Success x, _) -> f x
    | (Failure errs1, UnitType) ->
      assert (typeof<'T> = typeof<unit>) // runtime type is unit. So Unchecked.defaultof<'T> is not used inner f.
      match f (Unchecked.defaultof<'T>) with
      | Success _ -> Failure errs1
      | Failure errs2 -> Failure (NonEmptyList.append errs1 errs2)
    | (Failure xs, ValueType) -> Failure xs
  member __.Delay(f: unit -> AssertionResult<_>) = f
  member __.Run(f) = { Name = description; AssertionResult = f () }

let test description = TestBuilder(description)

type TrapBuilder () =
  member __.Zero () = ()
  member __.Delay(f: unit -> _) = f
  member __.Run(f) =
    try
      f () |> ignore
      Failure (NonEmptyList.singleton "Expect thrown exn but not")
    with
      e -> Success e

let trap = TrapBuilder ()
 
let inline checkWith returnValue expected actual =
  if expected = actual then Success returnValue
  else Failure (NonEmptyList.singleton (sprintf "Expect: %A\nActual: %A" expected actual))

let failure msg = Failure (NonEmptyList.singleton msg)
let success v = Success v

let check expected actual = checkWith actual expected actual
let assertEquals expected actual = checkWith () expected actual
