﻿namespace Persimmon

type BindingValue<'T> =
  | UnitAssertionResult of AssertionResult<'T (* unit *)>
  | NonUnitAssertionResult of AssertionResult<'T>
  | UnitTestCase of TestCase<'T (* unit *)>
  | NonUnitTestCase of TestCase<'T>

type TestBuilder(name: string) =
  member __.Return(x) = TestCase.make name [] (Passed x)
  member __.ReturnFrom(x: BindingValue<_>) =
    match x with
    | UnitAssertionResult x | NonUnitAssertionResult x -> TestCase.make name [] x
    | UnitTestCase x | NonUnitTestCase x -> TestCase<_>(name, x.Parameters, x.Run)
  member __.Source(x: AssertionResult<unit>) = UnitAssertionResult x
  member __.Source(x: AssertionResult<_>) = NonUnitAssertionResult x
  member __.Source(x: TestCase<unit>) = UnitTestCase x
  member __.Source(x: TestCase<_>) = NonUnitTestCase x
  member __.Bind(x, f: 'T -> TestCase<'U>) =
    match x with
    | UnitAssertionResult (Passed x)
    | NonUnitAssertionResult (Passed x) -> f x // TODO: try-with
    | UnitAssertionResult (NotPassed cause) ->
        assert (typeof<'T> = typeof<unit>)
        let res = f Unchecked.defaultof<'T> // TODO : try-with
        res |> TestCase.addNotPassed cause
    | NonUnitAssertionResult (NotPassed cause) -> TestCase.make name [] (NotPassed cause)
    | UnitTestCase case ->
        TestCase.combine (NoValueTest case) f
    | NonUnitTestCase case ->
        TestCase.combine (HasValueTest case) f

  member __.Delay(f) = f
  member __.Run(f) =
    try f ()
    with e -> TestCase.makeBreak name [] e

[<AutoOpen>]
module private Util =
  open Microsoft.FSharp.Reflection

  let toList (x: 'a) =
    if FSharpType.IsTuple typeof<'a> then
      FSharpValue.GetTupleFields (box x) |> Array.toList
    else
      [ box x ]

type ParameterizeBuilder() =
  member __.Delay(f: unit -> _) = f
  member __.Run(f) = f ()
  member __.Yield(()) = Seq.empty
  member __.Yield(x) = Seq.singleton x
  member __.YieldFrom(xs: _ seq) = xs
  [<CustomOperation("case")>]
  member inline __.Case(source, case) = seq { yield! source; yield case }
  [<CustomOperation("source")>]
  member __.Source (source1, source2) = Seq.append source1 source2
  [<CustomOperation("run")>]
  member __.RunTests(source: _ seq, f: _ -> TestCase<_>) =
    source
    |> Seq.map (fun x ->
        let ret = f x
        TestCase<_>({ ret.Metadata with Parameters = toList x }, ret.Run) :> TestObject)

type TrapBuilder () =
  member __.Zero () = ()
  member __.Delay(f: unit -> _) = f
  member __.Run(f) =
    try
      f () |> ignore
      fail "Expect thrown exn but not"
    with
      e -> pass e

[<AutoOpen>]
module Builder =
  let test name = TestBuilder(name)
  let parameterize = ParameterizeBuilder()
  let trap = TrapBuilder()