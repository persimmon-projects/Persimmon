namespace Persimmon.Tests

open System
open Persimmon
open UseTestNameByReflection
open Helper

module TrapTest =
  open System.Collections.Generic

  let ``return error if trap builder catch other exn`` =
    test {
      let d: Dictionary<string, obj> = null
      let! e = trap { it (d.Item("1")) }
      do! e.GetType() |> assertEquals typeof<KeyNotFoundException>
    }
    |> shouldFirstRaise<NullReferenceException, unit>
