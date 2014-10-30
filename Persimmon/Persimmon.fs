﻿module Persimmon

type ReturnType = UnitType | ValueType

type AssertionResult<'T> =
  | Passed of 'T
  | Failed of NonEmptyList<string>
  | Error of exn * string list
  | Skipped of string

module AssertionResult =
  let map f = function
  | Passed s -> Passed (f s)
  | Failed errs -> Failed errs
  | Error (e, errs) -> Error (e, errs)
  | Skipped reason -> Skipped reason

type TestResult<'T> = {
  Name: string
  AssertionResult: AssertionResult<'T>
}

module TestResult =
  let map f x =
    { Name = x.Name; AssertionResult = AssertionResult.map f x.AssertionResult }

type TestBuilder(description: string) =
  member __.Return(x) = Passed x
  member __.ReturnFrom(x, _) = x
  member __.Source(x: AssertionResult<unit>) = (x, UnitType)
  member __.Source(x: AssertionResult<_>) = (x, ValueType)
  member __.Source(x: TestResult<unit>) = (x.AssertionResult, UnitType)
  member __.Source(x: TestResult<_>) = (x.AssertionResult, ValueType)
  member __.Bind(x, f: 'T -> AssertionResult<_>) =
    match x with
    | (Passed x, _) -> f x
    | (Failed errs1, UnitType) ->
      assert (typeof<'T> = typeof<unit>) // runtime type is unit. So Unchecked.defaultof<'T> is not used inner f.
      try
        match f (Unchecked.defaultof<'T>) with
        | Passed _ -> Failed errs1
        | Failed errs2 -> Failed (NonEmptyList.append errs1 errs2)
        | Error (e, errs2) -> Error (e, List.append (errs1 |> NonEmptyList.toList) errs2)
        | Skipped msg -> Skipped msg
      with
        e -> Error (e, errs1 |> NonEmptyList.toList)
    | (Failed xs, ValueType) -> Failed xs
    | (Error (e, errs), _) -> Error (e, errs)
    | (Skipped msg, _) -> Skipped msg
  member __.Delay(f: unit -> AssertionResult<_>) = f
  member __.Run(f) = {
    Name = description
    AssertionResult = try f () with e -> Error (e, [])
  }

let test description = TestBuilder(description)

type TrapBuilder () =
  member __.Zero () = ()
  member __.Delay(f: unit -> _) = f
  member __.Run(f) =
    try
      f () |> ignore
      Failed (NonEmptyList.singleton "Expect thrown exn but not")
    with
      e -> Passed e

let trap = TrapBuilder ()
 
let inline checkWith returnValue expected actual =
  if expected = actual then Passed returnValue
  else Failed (NonEmptyList.singleton (sprintf "Expect: %A\nActual: %A" expected actual))

let fail msg = Failed (NonEmptyList.singleton msg)
let pass v = Passed v

let check expected actual = checkWith actual expected actual
let assertEquals expected actual = checkWith () expected actual

let skip reason x = { x with AssertionResult = Skipped reason }

type Append =
  | Append
  static member (?<-) (_: unit seq, Append, _: 'a seq) = fun (y: 'a) -> Seq.singleton y
  static member (?<-) (xs: ('a * 'b) seq, Append, _: ('a * 'b) seq) = fun (y: 'a * 'b) -> seq { yield! xs; yield y }

let inline append xs ys =
  (xs ? (Append) <- Seq.empty) ys

type ParameterizeBuilder() =
  member __.Delay(f: unit -> _) = f
  member __.Run(f) = f ()
  member __.Yield(x) = Seq.singleton x
  member __.YieldFrom(xs: _ seq) = xs
  member __.For(source : _ seq, body : _ -> _ seq) = source |> Seq.collect body
  [<CustomOperation("case")>]
  member inline __.Case(source, case) = append source case
  [<CustomOperation("run")>]
  member __.RunTests(source: _ seq, f: _ -> TestResult<_>) =
    source
    |> Seq.map (fun x -> let ret = f x in { ret with Name = sprintf "%s%A" ret.Name x })
  [<CustomOperation("source")>]
  member __.Source (_, source: seq<_>) = source

let parameterize = ParameterizeBuilder()
