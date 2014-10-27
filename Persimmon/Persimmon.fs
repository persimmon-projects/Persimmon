module Persimmon

type NonEmptyList<'T> = 'T * 'T list

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NonEmptyList =
  let cons head tail : NonEmptyList<_> = (head, tail)
  let singleton head : NonEmptyList<_> = (head, [])
  let append (xs: NonEmptyList<_>) (ys: NonEmptyList<_>) : NonEmptyList<_> =
    let x, xs = xs
    let y, ys = ys
    (x, (xs@(y::ys)))
  let iter action (list: NonEmptyList<'T>) =
    let head, tail = list
    action head
    List.iter action tail

type ReturnType = UnitType | ValueType

type AssertionResult<'T> =
  | Success of 'T
  | Failure of NonEmptyList<string>

type TestResult<'T> = {
  Name: string
  AssertionResult: AssertionResult<'T>
}

type TestBuilder(description: string) =
  member __.Return(()) = Success ()
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
      match f (Unchecked.defaultof<'T>) with
      | Success _ -> Failure errs1
      | Failure errs2 -> Failure (NonEmptyList.append errs1 errs2)
    | (Failure xs, ValueType) -> Failure xs
  member __.Delay(f: unit -> AssertionResult<_>) = f
  member __.Run(f) = { Name = description; AssertionResult = f () }

let test description = TestBuilder(description)
 
let inline checkWith returnValue expected actual =
  if expected = actual then Success returnValue
  else Failure (NonEmptyList.singleton (sprintf "Expect: %A\nActual: %A" expected actual))

let failure msg = Failure (NonEmptyList.singleton msg)
let success v = Success v

let check expected actual = checkWith actual expected actual
let assertEquals expected actual = checkWith () expected actual
