namespace Persimmon.Tests

open Persimmon
open UseTestNameByReflection

module MetadataTest =
  open Persimmon.TestResult

  let dummyRun _ = async {
    return endMarker.Box()
  }

  let ``should output full name`` = parameterize {
    source [
      TestCase.init (Some "test") [] dummyRun, "test"
      TestCase.init (Some "test") [ (typeof<int>, box 1) ] dummyRun, "test(1)"
      TestCase.init (Some "test") [ (typeof<int>, box 1); (typeof<string>, box "param") ] dummyRun, """test(1, "param")"""
    ]
    run (fun (metadata, expected) -> test {
      do! assertEquals expected <| sprintf "%A" metadata
    })
  }
