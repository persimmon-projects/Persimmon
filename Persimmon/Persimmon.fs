module Persimmon

type NonEmptyList<'T> = 'T * 'T list

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NonEmptyList =
  let iter action (list: NonEmptyList<'T>) =
    let head, tail = list
    action head
    List.iter action tail

type ReturnType = UnitType | ValueType

type AssertionResult<'T> =
  | Success of 'T
  | Failure of NonEmptyList<string>

type TestBuilder(description: string) =
  member __.Return(()) = Success ()
  member __.Return(x) = Success x
  member __.ReturnFrom(x, _) = x
  member __.Source(x: AssertionResult<unit>) = (x, UnitType)
  member __.Source(x: AssertionResult<_>) = (x, ValueType)
  member __.Bind(x, f: 'T -> AssertionResult<_>) =
    match x with
    | (Success x, _) -> f x
    | (Failure (res1, rest1), UnitType) ->
      match f (Unchecked.defaultof<'T>) with
      | Success _ -> Failure (res1, rest1)
      | Failure (res2, rest2) -> Failure (res1, rest1@(res2::rest2))
    | (Failure xs, ValueType) -> Failure xs
  member __.Delay(f: unit -> AssertionResult<_>) = f
  member __.Run(f) = f ()

let test description = TestBuilder(description)
 
let inline checkWith returnValue expected actual =
  if expected = actual then Success returnValue
  else Failure (sprintf "Expect: %A\nActual: %A" expected actual, [])

let failure msg = Failure (msg, [])
let success v = Success v

let check expected actual = checkWith actual expected actual
let assertEquals expected actual = checkWith () expected actual
