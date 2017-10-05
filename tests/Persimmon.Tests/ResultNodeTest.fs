module Persimmon.Tests.ResultNodeTest

open Persimmon
open Helper

let ``exception to strings`` = test "exception to strings" {
  let exceptionTest = test "exception test" {
    raise (exn("exception"))
    do! pass()
  }
  let actual =
    exceptionTest
    |> run
    |> ResultNode.toStrs 0
    |> Seq.toList
  do! assertPred (List.length actual > 2)
  do! assertEquals "FATAL ERROR: exception test" actual.[0]
  do! assertEquals (String.bar 70 '-' "exceptions") actual.[1]
}
