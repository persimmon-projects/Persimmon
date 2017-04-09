namespace Persimmon

open System
#if NET45 || NETSTANDARD
open System.Runtime.CompilerServices
#endif

/// This type is used only in the library.
type BindingValue<'T> =
  | UnitAssertionResult of AssertionResult<'T (* unit *)>
  | NonUnitAssertionResult of AssertionResult<'T>
  | UnitTestCase of TestCase<'T (* unit *)>
  | NonUnitTestCase of TestCase<'T>

type TestBuilder private (name: string option) =
  new() = TestBuilder(None)
  new(name: string) = TestBuilder(Some name)
  // return x
  member __.Return(x) = TestCase.makeDone name [] (Passed x)
  // return! x
  member __.ReturnFrom(x: BindingValue<_>) =
    match x with
    | UnitAssertionResult x | NonUnitAssertionResult x -> TestCase.makeDone name [] x
    | UnitTestCase x | NonUnitTestCase x -> TestCase<_>(name, x.Parameters, fun _ -> x.Run())
  // let! a = (x: AssertionResult<unit>) in ...
  member __.Source(x: AssertionResult<unit>, [<CallerLineNumber>]?line : int) =
    let x =
      match x with
      | NotPassed(None, cause) -> NotPassed(line, cause)
      | Passed _
      | NotPassed(Some _, _) -> x
    UnitAssertionResult x
  // let! a = (x: AssertionResult<_>) in ...
  member __.Source(x: AssertionResult<_>, [<CallerLineNumber>]?line : int) =
    let x =
      match x with
      | NotPassed(None, cause) -> NotPassed(line, cause)
      | Passed _
      | NotPassed(Some _, _) -> x
    NonUnitAssertionResult x
  // let! a = (x: TestCase<unit>) in ...
  member __.Source(x: TestCase<unit>) = UnitTestCase x
  // let! a = (x: TestCase<_>) in ...
  member __.Source(x: TestCase<_>) = NonUnitTestCase x
  // let! a = (x: BindingValue<_>) in ...
  member __.Bind(x, f: 'T -> TestCase<'U>) =
    match x with
    | UnitAssertionResult (Passed x) -> f x // TODO: try-with
    | NonUnitAssertionResult (Passed x) ->
      let c = f x // TODO: try-with
      match box x with
      | :? exn as e ->
        TestCase<_>(c.Name, c.Parameters, fun _ ->
          match c.Run() with
          | Done (_, (Passed _, []), _) as d -> d
          | Done (tc, assertionResults, duration) ->
            match assertionResults |> NonEmptyList.toSeq |> AssertionResult.Seq.onlyNotPassed |> Seq.toList with
            | [] -> failwith "oops!"
            | notPassed -> Error (tc, [e], notPassed, duration)
          | Error _ as e -> e
        )
      | _ -> c
    | UnitAssertionResult (NotPassed(line, cause)) ->
        assert (typeof<'T> = typeof<unit>)
        let res = f Unchecked.defaultof<'T> // TODO : try-with
        res |> TestCase.addNotPassed line cause
    | NonUnitAssertionResult (NotPassed(line, cause)) ->
      TestCase.makeDone name [] (NotPassed(line, cause))
    | UnitTestCase case ->
        TestCase.combine (NoValueTest case) f
    | NonUnitTestCase case ->
        TestCase.combine (HasValueTest case) f
  member inline this.Using(x: #IDisposable, f: #IDisposable -> TestCase<_>) =
    let dispose () = match box x with null -> () | _ -> x.Dispose()
    this.TryFinally((fun () -> f x), dispose)
  member __.TryFinally(f, g) =
    TestCase.init None [] (fun _ -> async {
      return
        try
          let case =
            try f ()
            with e -> TestCase.makeError None [] e
          case.Run()
        finally g ()
    })
  member __.Delay(f) = f
  member __.Run(f: unit -> TestCase<_>) =
    TestCase.init name [] (fun _ ->
      let case =
        try f ()
        with e -> TestCase.makeError name [] e
      case.AsyncRun()
    )

[<AutoOpen>]
module private Util =
  open Microsoft.FSharp.Reflection

  let toList (x: 'a) =
    if FSharpType.IsTuple typeof<'a> then
      FSharpValue.GetTupleFields (box x) |> Array.zip (FSharpType.GetTupleElements(typeof<'a>)) |> Array.toList
    else
      [ (typeof<'a>, box x) ]

type ParameterizeBuilder() =
  member __.Delay(f: unit -> _) = f
  member __.Run(f) =
    try
      f ()
    with e ->
      let e = exn("Failed to initialize `source` or `case` in `parameterize` computation expression.", e)
      TestCase.makeError None [] e
      |> Seq.singleton
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
      TestCase<_>(ret.Name, (toList x), fun _ -> ret.Run()))

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
    | Choice1Of2 r -> TestCase.makeDone None [] (Passed r)
    | Choice2Of2 e -> TestCase.makeError None [] e
