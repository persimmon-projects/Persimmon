namespace Persimmon

type ScriptTestBuilder internal (name: string) =
  let test = test name
  member __.Return(x) = test.Return(x)
  member __.ReturnFrom(x) = test.ReturnFrom(x)
  member __.Source(x: AssertionResult<unit>) = UnitAssertionResult x
  member __.Source(x: AssertionResult<_>) = NonUnitAssertionResult x
  member __.Source(x: TestCase<unit>) = UnitTestCase x
  member __.Source(x: TestCase<_>) = NonUnitTestCase x
  member __.Bind(x, f: 'T -> TestCase<'U>) = test.Bind(x, f)
  member __.Delay(f) = test.Delay(f)
  member __.Run(f) = test.Run(f).BoxTypeParam()

type ScriptParameterizeBuilder() =
  member __.Delay(f) = parameterize.Delay(f)
  member __.Run(f) =
    try
      f ()
    with e ->
      let e = exn("Failed to initialize `source` or `case` in `parameterize` computation expression.", e)
      [ TestCase.makeError None [] e ]
  member __.Yield(()) = parameterize.Yield(())
  member __.Yield(xs: _ seq) = parameterize.Yield(xs)
  [<CustomOperation("case")>]
  member inline __.Case(source, case) = parameterize.Case(source, case)
  [<CustomOperation("source")>]
  member __.Source(source1, source2) = parameterize.Source(source1, source2)
  [<CustomOperation("run")>]
  member __.RunTests(source, f: _ -> TestCase<'T>) =
    parameterize.RunTests(source, f)
    |> Seq.map (fun x -> (x :?> TestCase<'T>).BoxTypeParam())
    |> Seq.toList

module Helper =

  let countPassedOrSkipped xs = 
    xs
    |> List.filter (function
      | Done(_, xs, _) ->
        xs |> NonEmptyList.forall (function
          | Passed _
          | NotPassed (Skipped _) -> true
          | NotPassed (Violated _) -> false)
      | _ -> false)
    |> List.length

  let countNotPassedOrError xs = List.length xs - countPassedOrSkipped xs

module ScriptSyntax =

  let test name = ScriptTestBuilder(name)
  let parameterize = ScriptParameterizeBuilder()

type ScriptContext internal () =
  let onFinished = ref (List.iter (printfn "%A"))
with
  member this.OnFinished with get() = !onFinished and set(value) = onFinished := value
  member this.test(name) = ScriptSyntax.test name
  member this.parameterize = ScriptSyntax.parameterize
  member this.Run(f: ScriptContext -> TestCase<obj> list) =
    let res = f this |> List.map (fun t -> t.Run())
    this.OnFinished(res)

module ScriptRunner =

  let run f = ScriptContext().Run(f)
