namespace Persimmon

open System
open System.Diagnostics

// Utility functions of TestResult<'T>
module TestResult =

  let private endMarkerTestBody (tc:TestCase<unit>) : TestResult<unit> =
    new InvalidOperationException() |> raise
  let private endMarkerTestCase =
    new TestCase<unit>(Some "endMarker", [], endMarkerTestBody) :> ITestCase

  /// The marker represents the end of tests.
  /// The progress reporter needs the end marker in order to print new line at the end.
  let endMarker = {
      new ITestResult with
        member __.TestCase = endMarkerTestCase
        member __.Exceptions = [||]
        member __.Duration = TimeSpan.Zero
    }

  let addAssertionResult x = function
    | Done (metadata, (Passed _, []), d) -> Done (metadata, NonEmptyList.singleton x, d)
    | Done (metadata, results, d) -> Done (metadata, NonEmptyList.cons x results, d)
    | Error (metadata, es, results, d) -> Error (metadata, es, (match x with Passed _ -> results | NotPassed x -> x::results), d)

  let addAssertionResults (xs: NonEmptyList<AssertionResult<_>>) = function
    | Done (metadata, (Passed _, []), d) -> Done (metadata, xs, d)
    | Done (metadata, results, d) ->
      Done (metadata, NonEmptyList.appendList xs (results |> NonEmptyList.toList |> AssertionResult.List.onlyNotPassed |> NotPassedCause.List.toAssertionResultList), d)
    | Error (metadata, es, results, d) ->
      Error (metadata, es, (xs |> NonEmptyList.toList |> AssertionResult.List.onlyNotPassed)@results, d)

  let addDuration x = function
    | Done (metadata, results, d) -> Done (metadata, results, d + x)
    | Error (metadata, es, results, ts) -> Error (metadata, es, results, ts + x)
