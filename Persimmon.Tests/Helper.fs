﻿namespace Persimmon.Tests

open Persimmon

module Helper =

  let run (x: TestCase<_>) = x.Run()

  let shouldPassed<'T when 'T : equality> (expected: 'T) (x: TestCase<'T>) =
    let inner = function
    | Done (m, (Persimmon.Passed (actual: 'T), []), d) -> Done (m, (assertEquals expected actual, []), d)
    | Done (m, results, d) -> Done (m, results |> NonEmptyList.map (function
      | Passed _ -> Passed ()
      | NotPassed x -> NotPassed x), d)
    | Error (m, es, results, d) -> Error (m, es, results, d)
    TestCase({ Name = x.Name; Parameters = x.Parameters }, fun () -> inner (run x))

  let shouldNotPassed<'T> (expectedMessages: NonEmptyList<string>) (x: TestCase<'T>) =
    let inner = function
    | Done (m, (Persimmon.Passed (actual: 'T), []), d) ->
      Done (m, (fail (sprintf "Expect: Failure\nActual: %A" actual), []), d)
    | Done (m, results, d) ->
        results
        |> NonEmptyList.map (function NotPassed (Skipped x | Violated x) -> x | Persimmon.Passed x -> sprintf "Expected is NotPased but Passed(%A)" x)
        |> fun actual -> Done (m, (assertEquals expectedMessages actual, []), d)
    | Error (m, es, results, d) -> Error (m, es, results, d)
    TestCase({ Name = x.Name; Parameters = x.Parameters }, fun () -> inner (run x))
