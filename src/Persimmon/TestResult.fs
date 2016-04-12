namespace Persimmon

open System
open System.Diagnostics

// Utility functions of TestResult<'T>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TestResult =

  let private endMarkerTestBody (tc:TestCase<unit>) : TestResult<unit> =
    new InvalidOperationException() |> raise
  let private endMarkerTestCase =
    new TestCase<unit>(Some "endMarker", [], endMarkerTestBody) :> TestCase

  /// The marker represents the end of tests.
  /// The progress reporter needs the end marker in order to print new line at the end.
  let endMarker = {
      new TestResult with
        member __.TestCase = endMarkerTestCase
        member __.Exceptions = [||]
        member __.Duration = TimeSpan.Zero
        member __.Results = [||]
    }

  let addAssertionResult x = function
    | Done (testCase, (Passed _, []), d) -> Done (testCase, NonEmptyList.singleton x, d)
    | Done (testCase, results, d) -> Done (testCase, NonEmptyList.cons x results, d)
    | Error (testCase, es, results, d) -> Error (testCase, es, (match x with Passed _ -> results | NotPassed x -> x::results), d)

  let addAssertionResults (xs: NonEmptyList<AssertionResult<_>>) = function
    | Done (testCase, (Passed _, []), d) -> Done (testCase, xs, d)
    | Done (testCase, results, d) ->
      Done (testCase, NonEmptyList.appendList xs (results |> NonEmptyList.toList |> AssertionResult.Seq.onlyNotPassed |> NotPassedCause.Seq.toAssertionResultList), d)
    | Error (testCase, es, results, d) ->
      Error (testCase, es, (xs |> NonEmptyList.toSeq |> AssertionResult.Seq.onlyNotPassed |> Seq.toList)@results, d)

  let addDuration x = function
    | Done (testCase, results, d) -> Done (testCase, results, d + x)
    | Error (testCase, es, results, ts) -> Error (testCase, es, results, ts + x)
