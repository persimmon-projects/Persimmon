module Persimmon.Sample

open Persimmon

let test1 = test "success test" {
  do! assertEquals 1 1
}

let test2 = test "failure test" {
  do! assertEquals 1 2
}

exception MyException

let test3 = test "exn test" {
  let! e = trap { raise MyException }
  do! assertEquals "" e.Message
  do! assertEquals typeof<MyException> (e.GetType())
  do! assertEquals "" (e.StackTrace.Substring(0, 5))
}

let err () = failwith "oops!"

let test4 = test "error test" {
  do! assertEquals 1 2
  do! assertEquals (err ()) 1
}

let tests = [
  test "success test(list)" {
    do! assertEquals 1 1
  }
  test "failure test(list)" {
    do! assertEquals 1 2
  }
]

let tests2 = [|
  test "success test1(array)" {
    do! assertEquals 1 1
  }
  test "success test2(array)" {
    do! assertEquals 1 1
  }
  test "failure test(array)" {
    do! assertEquals 1 2
  }
|]

let tests3 = seq {
  yield test "failure test(seq)" {
    do! assertEquals 1 2
  }
}

let tests4 = [
  test "success test(list with value)" {
    return 10
  }
  test "failure test(list with value)" {
    do! assertEquals 1 2
    return 20
  }
]

let tests5 =
  let parameterizeTest (x, y) = test "case parameterize test" {
    do! assertEquals x y
  }
  parameterize {
    case (1, 1)
    case (1, 2)
    run parameterizeTest
  }

let tests6 =
  let parameterizeTest (x, y) = test "source parameterize test" {
    do! assertEquals x y
  }
  let parameterizeTest2 (x, y) = test "source parameterize test2" {
    do! assertEquals (x + 1) y
  }
  parameterize {
    source [
      (1, 1)
      (1, 2)
    ]
    run parameterizeTest
    run parameterizeTest2
  }

type MyClass() =
  member __.test() = test "not execute because this is instance method" {
    do! assertEquals 1 2
  }
  static member test2 = test "failure test(static property)" {
    do! assertEquals 1 2
  }
