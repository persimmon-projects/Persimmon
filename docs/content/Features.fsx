(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use
// it to define helpers that you do not want to show in the documentation.
#I "../../Persimmon/bin/Release"
#I "../../Persimmon.Runner/bin/Release"
#r "Persimmon"
open Persimmon

type Provider() =
  member __.getSomeData(_) = ""

let getProvider _ = Provider()

(**
<div class="blog-post">

Features
========

Using Computation Expressions
-----------------------------

Persimmon uses Computation Expressions to:

* write a test
* write a parameterized test
* specify the expected exception

By using Computation Expressions, we can write the tests more concisely, flexibly, and safely than the other existing testing frameworks for F#.

*)

let f n =
  if n > 0 then n * 2
  else failwith "oops!"

let tests = [
  test "(f 21) should be equal to 42" {
    do! assertEquals 42 (f 21)
  }
  test "(f 0) should raise exn" {
    let! e = trap { it (f 0) }
    do! assertEquals (typeof<exn>) (e.GetType())
  }
]

(**

Mark a test
-----------

In many testing frameworks, they mark tests with attributes or by their naming rules.
However, Persimmon doesn't use them to mark the tests.

Persimmon marks variables, properties, and methods (only ``unit`` argument) whose return types are ``TestObject`` or its subtypes.

Composable test
---------------

A test of Persimmon is composable.

It can accept several tests and return a new test.

*)

let getSomeData x y = test "" {
  let provider = getProvider x
  let res = provider.getSomeData y
  do! assertNotEquals "" res
  return res
}

let composedTests = [
  test "test1" {
    let! data = getSomeData [(10, "ten"); (3, "three")] 3
    do! assertEquals "three" data
  }
  test "test2" {
    let! data = getSomeData [(10, "ten"); (3, "three")] 10
    do! assertEquals "ten" data
  }
]

(**

Continuable assertion
---------------------

An assetion of Persimmon (doesn't have a result) continues to execute remaining assertions even if it violated.
And Persimmon can enumerate all violated assertions in the test.
This behaviour brings the advantage that a test doesn't sacrifice the amount of information and can be simplified to write it.

*)

// This test reports two assertion violations.
let ``some assertions test`` = [
  test "test1" {
    do! assertEquals 1 2 // execute and violate
    do! assertEquals 2 2 // continue to execute and pass
    do! assertEquals 3 4 // continue to execute and violate
  }
]

(**

</div>
*)
