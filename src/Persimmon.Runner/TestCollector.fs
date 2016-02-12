module Persimmon.Runner.TestCollector

open System.Reflection
open Persimmon.Internals

let collectRootTestObjects (asms: Assembly list) =
  let collector = TestCollector()
  asms
  |> Seq.collect collector.Run
  |> Seq.toList
