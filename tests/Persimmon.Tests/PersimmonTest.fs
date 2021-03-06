﻿namespace Persimmon.Tests

open Persimmon
open Helper

module PersimmonTest =

  let ``'pass' function should always pass`` =
    test "'pass' function should always pass" {
      return! pass 1
    }
    |> shouldPassed 1

  let ``'pass' function should always pass in do-bang notation`` =
    test "'pass' function should always pass in do-bang notation" {
      do! pass ()
    }
    |> shouldPassed ()

  let ``'fail' function should always fail`` =
    let msg = "always fail"
    test "'fail' function should always fail" {
      return! fail msg
    }
    |> shouldNotPassed (NonEmptyList.singleton msg)

  let ``all unit type assertion should run`` =
    test "all unit type assertion should run" {
      do! assertEquals 1 2
      do! assertEquals 2 2
      do! assertEquals 3 4
    }
    |> shouldNotPassed<unit> (NonEmptyList.make "Expect: 1\nActual: 2" [ "Expect: 3\nActual: 4" ])

  let table = Map.ofList [("x", "y"); ("y", "z"); ("z", "other")]

  let ``Persimmon test should be able to compose`` =
    let test1 = test "pass" {
      let value = table |> Map.find "x"
      do! assertEquals "y" value
      return value
    }
    let test2 = test "more pass" {
      let! res = test1
      let value = table |> Map.find res
      do! assertEquals "z" value
      return value
    }
    let test3 = test "fail" {
      let! res = test2
      let value = table |> Map.find res
      do! assertEquals "x" value
      return value
    }
    let test4 = test "more fail" {
      let! res = test3
      let value = table |> Map.find res
      do! assertEquals "y" value // not execute
      return value
    }

    test "Persimmon test should be able to compose" {
      do! test1 |> shouldPassed "y"
      do! test2 |> shouldPassed "z"
      do! test3 |> shouldNotPassed (NonEmptyList.singleton "Expect: \"x\"\nActual: \"other\"")
      do! test4 |> shouldNotPassed (NonEmptyList.singleton "Expect: \"x\"\nActual: \"other\"")
    }

  let ``parameterize tests should be able to run`` =
    let innerTests =
      parameterize {
        source [
          (1, 1)
          (1, 2)
        ]
        run (fun (x, y) -> test "source parameterize test" {
          do! assertEquals x y
        })
      }
    test "parameterize tests should be able to run" {
      do! innerTests |> shouldEqualErrorCount 1
    }

  open ActivePatterns

  let ``catch parameterized test error`` =

    let getTestCase xs =
      xs |> Seq.choose (function TestCase c -> Some (c.Box()) | _ -> None) |> Seq.head

    let x () = 3
    let y () = failwith "error"

    let stub x = test "not execute" {
      do! x |> assertEquals 3
    }

    let sourcePattern =
      parameterize {
        source [
          x ()
          y ()
        ]
        run stub
      }
      |> getTestCase

    let casePattern =
      parameterize {
        case (x ())
        case (y ())
        run stub
      }
      |> getTestCase

    parameterize {
      source [
        sourcePattern
        casePattern
      ]
      run (fun error -> test "catch parameterized test error" {
        do! shouldFirstRaise<exn, obj> error
      })
    }

  let ``ordering parameters`` =
    let getMetadata parameters =
      parameters
      |> Seq.map (function
      | Context _ -> failwith "oops!"
      | TestCase tc -> tc.Parameters)
      |> Seq.head
    let parameter1 =
      parameterize {
        source [
          (1, 2)
        ]
        run (fun (y, x) -> test "parameter1" {
          do! assertEquals x y
        })
      }
      |> getMetadata
    let parameter2 =
      parameterize {
        case (1, 2, 5)
        run (fun (b, c, a) -> test "parameter2" {
          do! assertEquals a (b + c)
        })
      }
      |> getMetadata
    let parameter3 =
      parameterize {
        source [ (1, 2, 5) ]
        run (fun (b, c, a) -> test "parameter3" {
          do! assertEquals a (b + c)
        })
      }
      |> getMetadata
    test "ordering parameters" {
      do! assertEquals "1, 2" (PrettyPrinter.printAll parameter1)
      do! assertEquals "1, 2, 5" (PrettyPrinter.printAll parameter2)
      do! assertEquals "1, 2, 5" (PrettyPrinter.printAll parameter3)
    }

  type DisposableValue() =
    let disposed = ref false
    member __.Disposed = !disposed
    interface System.IDisposable with
      member __.Dispose() = disposed := true

  let ``should dispose value`` =
    let test1 = test "return disposed value" {
      use s = new DisposableValue()
      return s
    }
    test "should dispose value" {
      let! value = test1
      do! assertPred (value.Disposed)
    }

  exception TestException

  let ``should dispose finally`` =
    let value = new DisposableValue()
    let test1 () = test "use and exception" {
      use _ = value
      return raise TestException
    }
    test "should dispose finally" {
      do! assertPred (not <| value.Disposed)
      let ex = test1 ()
      do! shouldFirstRaise<TestException, unit> ex
      do! assertPred value.Disposed
    }

  let ``should not dispose until the end of test(issue #117)`` =
    test "should not dispose until the end of test(issue #117)" {
      use disposable = new DisposableValue()
      let subtest =
        test "subtest" {
          return
            if disposable.Disposed then
              raise TestException
        }
      do! subtest
      do! disposable.Disposed |> assertEquals false
    }

  let ``check try finally`` =
    let count = ref 0
    let test1 () = test "use and exception" {
      try
        return raise TestException
      finally
       incr count
    }
    test "check try finally" {
      let ex = test1 ()
      do! shouldFirstRaise<TestException, unit> ex
      do! assertEquals 1 !count
    }

  let ``should not run finally expression until the end of test`` =
    test "should not run finally expression until the end of test" {
      let disposable = new DisposableValue()
      try
        do! test "empty" { return () }
        do! disposable.Disposed |> assertEquals false
      finally
        (disposable :> System.IDisposable).Dispose()
    }

  let ``first binding exception`` =
    let raiseTest = test "raise exception" {
      return raise TestException
    }
    let bindingTest = test "bind and fail" {
      do! raiseTest
      do! fail "fail test"
    }
    test "first binding exception" {
      do! shouldFirstRaise<TestException, unit> bindingTest
    }

  // bug #135
  let ``can fail with exception`` =
    test "can fail with exception" {
      do raise TestException
      do! fail "fail test"
    }
    |> skip "skip exception test"