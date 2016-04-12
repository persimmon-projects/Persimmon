namespace Persimmon.Tests

open Persimmon
open UseTestNameByReflection

module MetadataTest =

  let ``should output full name`` = parameterize {
    source [
      TestCase.initForSynch (Some "test") [], "test"
      TestCase.initForSynch (Some "test") [ (typeof<int>, box 1) ], "test(1)"
      TestCase.initForSynch (Some "test") [ (typeof<int>, box 1); (typeof<string>, box "param") ], """test(1, "param")"""
    ]
    run (fun (metadata, expected) -> test {
      do! assertEquals expected <| sprintf "%A" metadata
    })
  }
