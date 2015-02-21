﻿namespace Persimmon

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
  member __.Yield(()) = (Empty, Seq.empty)
  member __.Yield(x) = (Empty, Seq.singleton x)
  [<CustomOperation("case")>]
  member inline __.Case((action, source), case) = (action, seq { yield! source; yield case })
  [<CustomOperation("source")>]
  member __.Source ((action, source1), source2) = (action, Seq.append source1 source2)
  [<CustomOperation("run")>]
  member __.RunTests((action, source: _ seq), f: _ -> TestCase<'T>) =
    source
    |> Seq.map (fun x ->
      let ret = TestBuilder("") {
        do match action with | Before b | BeforeAfter(b, _) -> b () | _ -> ()
        let ret = f x
        do match action with | After a | BeforeAfter(_, a) -> a () | _ -> ()
        return! ret
      }
      let metadata = { ret.Metadata with Parameters = x |> toList }
      TestCase<_>(metadata, ret.Run).BoxTypeParam() :> TestObject)
  [<CustomOperation("before")>]
  member __.Before((action, source: _ seq), before: unit -> unit) =
    let action =
      match action with
      | Empty -> Before before
      | Before b1 -> Before (b1 >> before)
      | After after -> BeforeAfter(before, after)
      | BeforeAfter(b1, after) -> BeforeAfter(b1 >> before, after)
    (action, source)
  [<CustomOperation("after")>]
  member __.After((action, source: _ seq), after: unit -> unit) =
    let action =
      match action with
      | Empty -> After after
      | Before before -> BeforeAfter(before, after)
      | After a1 -> After (a1 >> after)
      | BeforeAfter(before, a1) -> BeforeAfter(before, a1 >> after)
    (action, source)

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

type AsyncRunBuilder() =
  member __.Yield(()) = ()
  [<CustomOperation("it")>]
  member __.It((), a: Async<'T>) = a
  member __.Run(a) =
    match a |> Async.Catch |> Async.RunSynchronously with
    | Choice1Of2 r -> TestCase.make "" [] (Passed r)
    | Choice2Of2 e -> TestCase.makeError "" [] e 

type TestWithBeforeOrAfterBuilder (name: string, before: (unit -> unit) option, after: (unit -> unit) option) =
  let test = TestBuilder(name)
  member __.Return(x) = test.Return(x)
  member __.ReturnFrom(x) = test.ReturnFrom(x)
  member __.Source(x: AssertionResult<unit>) = UnitAssertionResult x
  member __.Source(x: AssertionResult<_>) = NonUnitAssertionResult x
  member __.Source(x: TestCase<unit>) = UnitTestCase x
  member __.Source(x: TestCase<_>) = NonUnitTestCase x
  member __.Bind(x, f: 'T -> TestCase<'U>) = test.Bind(x, f)
  member __.Delay(f) = test.Delay(f)
  member __.Run(f: unit -> TestCase<'T>) =
    TestCaseWithBeforeOrAfter(test {
      before |> Option.iter (fun f -> f ())
      let res = f ()
      after |> Option.iter (fun f -> f ())
      return! res.BoxTypeParam()
    })
