module Persimmon.FsCheck

open Persimmon
open FsCheck

exception internal FsCheckFailException of string

let runner =
  { new IRunner with
    member x.OnStartFixture t = ()
    member x.OnArguments(ntets: int, args: obj list, every: int -> obj list -> string) = ()
    member x.OnShrink(args, everyShrink) = ()
    member x.OnFinished(name, result) =
      match result with
      | TestResult.True _ -> ()
      | _ -> raise (FsCheckFailException(Runner.onFinishedToString name result)) }

let config = { Config.Default with Runner = runner }

let inline private checkWithName name testable =
  try
    Check.One(name, config, testable)
    TestCase.make name [] (Passed ())
  with
    | FsCheckFailException s -> TestCase.make name [] (NotPassed (Violated s))
    | e -> TestCase.makeError name [] e

type PropertyBuilder(name: string) =
  let test = TestBuilder(name)
  member __.Return(()) = TestCase.make name [] (Passed ())
  member __.Yield(()) = TestCase.make name [] (Passed ())
  member __.Source(x: BindingValue<unit>) = x
  member __.Source((name, testable)) = UnitTestCase(checkWithName name testable)
  member __.Bind(x, f) = test.Bind(x, f)
  [<CustomOperation("check")>]
  member __.Check(_: TestCase<unit>, testable) =
    checkWithName "" testable
  member __.Delay(f) = test.Delay(f)
  member __.Run(f) = test.Run(f)

let prop name = PropertyBuilder(name)
