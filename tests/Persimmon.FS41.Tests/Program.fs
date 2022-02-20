open Persimmon.Console
open Persimmon.Tests


[<EntryPoint>]
let main argv =
  let args = Args.parse Args.empty (argv |> Array.toList)
  let tests = seq {
    yield! AssertionTest.``get line number``
  }
  Runner.runTests args tests