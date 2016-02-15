module Persimmon.Runner.TestCollector

open System.Reflection
open Persimmon
open Persimmon.Internals

let collectRootTestObjects (asms: Assembly seq) =
  let collector = TestCollector()
  asms
  |> Seq.collect collector.Run
  |> Seq.toList
