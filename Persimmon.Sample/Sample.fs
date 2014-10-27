module Persimmon.Sample

open Persimmon

let test1 = test "success" {
  do! assertEquals 1 1
}

let test2 = test "failure" {
  do! assertEquals 1 2
}