namespace Persimmon

/// This type is used only in the library.
type BindingValue<'T> =
  | UnitAssertionResult of AssertionResult<'T (* unit *)>
  | NonUnitAssertionResult of AssertionResult<'T>
  | UnitTestCase of TestCase<'T (* unit *)>
  | NonUnitTestCase of TestCase<'T>

type TestBuilder(name: string) =
  // return x
  member __.Return(x) = TestCase.make name [] (Passed x)
  // return! x
  member __.ReturnFrom(x: BindingValue<_>) =
    match x with
    | UnitAssertionResult x | NonUnitAssertionResult x -> TestCase.make name [] x
    | UnitTestCase x | NonUnitTestCase x -> TestCase<_>(name, x.Parameters, x.Run)
  // let! a = (x: AssertionResult<unit>) in ...
  member __.Source(x: AssertionResult<unit>) = UnitAssertionResult x
  // let! a = (x: AssertionResult<_>) in ...
  member __.Source(x: AssertionResult<_>) = NonUnitAssertionResult x
  // let! a = (x: TestCase<unit>) in ...
  member __.Source(x: TestCase<unit>) = UnitTestCase x
  // let! a = (x: TestCase<_>) in ...
  member __.Source(x: TestCase<_>) = NonUnitTestCase x
  // let! a = (x: BindingValue<_>) in ...
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
    with e -> TestCase.makeError name [] e

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
        let metadata = { ret.Metadata with Parameters = x |> toList }
        TestCase<_>(metadata, ret.Run) :> TestObject)

type TrapBuilder () =
  member __.Zero () = ()
  member __.Yield(()) = Seq.empty
  [<CustomOperation("it")>]
  member __.It(_, f) = f
  member __.Delay(f: unit -> _) = f
  member __.Run(f) =
    try
      f () |> ignore
      fail "Expect thrown exn but not"
    with
      e -> pass e
