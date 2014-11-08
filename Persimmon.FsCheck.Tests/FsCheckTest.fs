namespace Persimmon.FsCheck.Tests

open Persimmon
open Persimmon.FsCheck

module FsCheckTest =

  let run (x: TestCase<_>) = x.Run()

  let shouldPassed (x: TestCase<unit>) =
    let inner = function
    | Done (m, (Passed (), [])) -> Done (m, NonEmptyList.singleton (Passed ()))
    | Done (m, results) -> Done (m, results |> NonEmptyList.map (function
      | Passed _ -> Passed ()
      | NotPassed x -> NotPassed x))
    | Error (m, e, results) -> Error (m, e, results)
    TestCase({ Name = x.Name; Parameters = x.Parameters }, fun () -> inner (run x))

  let shouldNotPassed (x: TestCase<unit>) =
    let inner = function
    | Done (m, (Passed (), [])) ->
      Done (m, NonEmptyList.singleton (fail "Expect: Failure\nActual: Success"))
    | Done (m, results) ->
        results
        |> NonEmptyList.map (function NotPassed (Skipped x | Violated x) -> x | Persimmon.Passed x -> sprintf "Expected is NotPased but Passed(%A)" x)
        |> NonEmptyList.length
        |> fun x -> Done(m, NonEmptyList.singleton (assertPred (x >= 1)))
    | Error (m, e, results) -> Error (m, e, results)
    TestCase({ Name = x.Name; Parameters = x.Parameters }, fun () -> inner (run x))

  let ``simple success prop should succeed`` () =
    prop "simple success prop should succeed" {
      check (fun _ -> true)
    }
    |> shouldPassed

  let ``simple failure prop should fail`` () =
    prop "simple failure prop should fail" {
      check (fun _ -> false)
    }
    |> shouldNotPassed

  let ``multiple prop test``() =
    prop "list monoid laws" {
      do! "left identity", fun (x: int list) -> (List.append [] x) = x
      do! "right identity", fun (x: int list) -> List.append x [] = x
      do! "associative", fun (x: int list) (y: int list) (z: int list) ->
        List.append x (List.append y z) = List.append (List.append x y) z
    }
    |> shouldPassed
