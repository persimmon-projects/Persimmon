namespace Persimmon.Tests

open Persimmon
open UseTestNameByReflection
open Helper

module SideEffectTest =

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

  let ``side effect should execute once`` = test {
    do! ``hoge fuga test`` |> shouldPassed ()
    do! assertEquals 1 !sideEffect
  }

  let sideEffect2 = ref 0

  let ``reuse test with`` (x: 'T) = test {
    incr sideEffect2
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

  let ``side effect should execute twice`` = test {
    do! ``foo bar test`` |> shouldPassed ()
    do! assertEquals 2 !sideEffect2
  }

  let ``before Effect test`` () =
    let beforeEffect = ref 0
    testWithBefore (fun () -> incr beforeEffect) {
      do! assertEquals 1 !beforeEffect
    }

  let ``after Effect test`` =
    let afterEffect = ref 0
    let inner =
      testWithAfter (fun () -> incr afterEffect) {
        return ()
      }
    test {
      do! assertEquals 1 !afterEffect
    }
