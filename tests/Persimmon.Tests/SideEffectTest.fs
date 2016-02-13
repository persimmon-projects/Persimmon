namespace Persimmon.Tests

open Persimmon
open UseTestNameByReflection
open Helper

module SideEffectTest =

  let ``side effect should execute once`` =

    let sideEffect = ref 0

    let ``reuse test`` = test {
      incr sideEffect
      return 10
    }

    let hoge = test {
      let! x = ``reuse test``
      do! assertEquals 10 x
    }

    let fuga = test {
      let! x = ``reuse test``
      do! assertEquals 10 x
    }

    let ``hoge fuga test`` = test {
      do! hoge
      do! fuga
    }

    test {
      do! ``hoge fuga test`` |> shouldPassed ()
      do! assertEquals 1 !sideEffect
    }

  let ``side effect should execute twice`` =

    let sideEffect = ref 0

    let ``reuse test with`` (x: 'T) = test {
      incr sideEffect
      return x
    }

    let foo = test {
      let! x = ``reuse test with`` 10
      do! assertEquals 10 x
    }

    let bar = test {
      let! x = ``reuse test with`` 20
      do! assertEquals 20 x
    }

    let ``foo bar test`` = test {
      do! foo
      do! bar
    }

    test {
      do! ``foo bar test`` |> shouldPassed ()
      do! assertEquals 2 !sideEffect
    }

  let ``let! and sequence`` =
 
    let aCount = ref 0
    let a = test {
      do incr aCount
      return sprintf "acount: %d" !aCount
    }

    let bCount = ref 0
    let b = test {
      let! a = a
      incr bCount
      return sprintf "%s, bcount: %d" a !bCount
    }

    let f () = test {
      let! b = b
      do! assertEquals 1 !aCount
      do! assertEquals 1 !bCount
    }

    test {
      do! f ()
      do! f ()
    }

  let ``do! and return`` =
 
    let sideEffect = ref 0

    let ``reuse test`` = test {
      do! assertPred true
      return incr sideEffect
    }

    let f () = test {
      do! ``reuse test``
      do! assertEquals 1 !sideEffect
    }

    test {
      do! f ()
      do! f ()
    }

  type DisposableValue(value: int) =
    member __.Value = value
    interface System.IDisposable with
      member __.Dispose() = ()

  let ``use! and sequence`` =

    let aCount = ref 0
    let disposableValueTest = test {
      incr aCount
      return new DisposableValue(10)
    }

    let bCount = ref 0
    let b = test {
      use! value = disposableValueTest
      incr bCount
      return value.Value
    }

    let f () = test {
      let! b = b
      do! assertEquals 1 !aCount
      do! assertEquals 1 !bCount
    }

    test {
      do! f ()
      do! f ()
    }
