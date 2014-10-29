module Persimmon.Sample

open Persimmon

let test1 = test "success test" {
  do! assertEquals 1 1
}

let test2 = test "failure test" {
  do! assertEquals 1 2
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

let test4 = [
  test "success test(list with value)" {
    return 10
  }
  test "failure test(list with value)" {
    do! assertEquals 1 2
    return 20
  }
]

type MyClass() =
  member __.test() = test "not execute because this is instance method" {
    do! assertEquals 1 2
  }
  static member test2 = test "failure test(static property)" {
    do! assertEquals 1 2
  }