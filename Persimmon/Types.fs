﻿namespace Persimmon

open System
open System.Diagnostics

/// The cause of not passed assertion.
type NotPassedCause =
    /// The assertion is not passed because it is skipped.
  | Skipped of string
    /// The assertion is not passed because it is violated.
  | Violated of string

/// The result of each assertion.
type AssertionResult<'T> =
    /// The assertion is passed.
  | Passed of 'T
    /// The assertion is not passed.
  | NotPassed of NotPassedCause

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NotPassedCause =
  module List =
    let toAssertionResultList xs = xs |> List.map NotPassed

module AssertionResult =
  module List =
    let onlyNotPassed xs =
      xs |> List.choose (function NotPassed x -> Some x | _ -> None)

  module NonEmptyList =
    /// Calculate the typical assertion result.
    /// It returns most important result.
    /// For example, "Violated" is more important than "Passed"
    let typicalResult xs =
      xs
      |> NonEmptyList.reduce (fun acc x ->
          match acc, x with
          | NotPassed (Violated _), _
          | NotPassed (Skipped _), NotPassed (Skipped _)
          | NotPassed (Skipped _), Passed _
          | Passed _, Passed _ -> acc
          | _, _ -> x
      )

/// The metadata that is common to each test case and test result.
type TestMetadata = {
  /// The test name. It doesn't contain the parameters.
  Name: string
  /// The test parameters.
  /// If the test has no parameters then the value is empty list.
  Parameters: obj list
}
with
  /// The test name(if the test has parameters then the value contains them).
  member this.FullName = this.ToString()
  override this.ToString() =
    if this.Parameters.IsEmpty then
      this.Name
    else
      sprintf "%s(%s)" this.Name (this.Parameters |> List.map string |> String.concat ", ")

/// The type that is treated as tests by Persimmon.
/// Derived class of this class are only two classes,
/// they are Context and TestCase<'T>.
/// When the TestObject is executed becomes TestResult.
/// You should use the ActivePatterns
/// if you want to process derived objects through this class.
[<AbstractClass>]
type TestObject internal () =
  abstract member SetNameIfNeed: string -> TestObject

/// This marker interface represents a test result.
/// You should use the ActivePatterns
/// if you want to process derived objects through this interface.
type ITestResult = interface end

/// This class represents a nested test.
/// We can use this class for grouping of the tests.
type Context(name: string, children: TestObject list) =
  inherit TestObject ()

  override __.SetNameIfNeed(newName: string) =
    Context((if name = "" then newName else name), children) :> TestObject

  /// The context name.
  member __.Name = name
  /// This is a list that has the elements represented the subcontext or the test case.
  member __.Children = children

  /// Execute tests recursively.
  member __.Run(reporter: ITestResult -> unit) =
    { Name = name
      Children =
        children
        |> List.map (function
                     | :? Context as c ->
                         let res = c.Run(reporter) :> ITestResult
                         reporter res
                         res
                     | x (* :? TestCase<_> *) ->
                         let run = x.GetType().GetMethod("Run")
                         let res = run.Invoke(x, [||]) :?> ITestResult
                         reporter res
                         res) }

  override this.ToString() =
    sprintf "Context(%A, %A)" name children

/// This class represents a nested test result.
/// After running tests, the Context objects become the ContextReults objects.
and ContextResult = {
  Name: string
  Children: ITestResult list
}
with
  override this.ToString() = sprintf "%A" this
  interface ITestResult

/// This class represents a test that has not been run yet.
/// In order to run the test represented this class, use the "Run" method.
type TestCase<'T>(metadata: TestMetadata, body: unit -> TestResult<'T>) =
  inherit TestObject ()

  new (name, parameters, body) = TestCase<_>({ Name = name; Parameters = parameters }, body)

  override __.SetNameIfNeed(newName: string) =
    TestCase<'T>({ metadata with Name = if metadata.Name = "" then newName else metadata.Name }, body) :> TestObject

  member internal __.Metadata = metadata

  /// The test name. It doesn't contain the parameters.
  member __.Name = metadata.Name
  /// The test name(if the test has parameters then the value contains them).
  member __.FullName = metadata.FullName
  /// The test parameters.
  /// If the test has no parameters then the value is empty list.
  member __.Parameters = metadata.Parameters
  /// Execute the test.
  member __.Run() = 
    let watch = Stopwatch.StartNew()
    let result = body ()
    watch.Stop()
    match result with
    | Error (_, errs, res, d) -> Error (metadata, errs, res, d + watch.Elapsed)
    | Done (_, res, d) -> Done (metadata, res, d + watch.Elapsed)

  override __.ToString() =
    sprintf "TestCase<%s>(%A)" (typeof<'T>.Name) metadata

/// The result of each test.
/// After running tests, the TestCase objects become the TestResult objects.
and TestResult<'T> =
    /// This case represents the error.
  | Error of TestMetadata * exn list * NotPassedCause list * TimeSpan
    /// This case represents that all of the assertions is finished.
  | Done of TestMetadata * NonEmptyList<AssertionResult<'T>> * TimeSpan
  with
    member private this.Metadata =
      match this with Error (x, _, _, _) | Done (x, _, _) -> x
    /// The test name. It doesn't contain the parameters.
    member this.Name = this.Metadata.Name
    /// The test name(if the test has parameters then the value contains them).
    member this.FullName = this.Metadata.FullName
    /// The test parameters.
    /// If the test has no parameters then the value is empty list.
    member this.Parameters = this.Metadata.Parameters

    /// Convert TestResult<'T> to TestResult<obj>.
    member this.BoxTypeParam() =
      match this with
      | Error (meta, es, res, d) -> Error (meta, es, res, d)
      | Done (meta, res, d) ->
          Done (meta, res |> NonEmptyList.map (function Passed x -> Passed (box x) | NotPassed x -> NotPassed x), d)

    interface ITestResult

// extension
type TestCase<'T> with
  /// Convert TestCase<'T> to TestCase<obj>.
  member this.BoxTypeParam() =
    TestCase<obj>(this.Metadata, fun () -> this.Run().BoxTypeParam())

// Utility functions of TestResult<'T>
module TestResult =
  /// The marker represents the end of tests.
  /// The progress reporter needs the end marker in order to print new line at the end.
  let endMarker = { new ITestResult }

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

/// This DU represents the type of the test case.
/// If the test has some return values, then the type of the test case is HasValueTest.
/// If not, then it is NoValueTest.
type TestCaseType<'T> =
    /// The TestCase does not have any return values.
    /// It means that the TestCase is TestCase<unit>.
  | NoValueTest of TestCase<'T>
    /// The TestCase has some return values.
    /// It means that the TestCase is not TestCase<unit>.
  | HasValueTest of TestCase<'T>

module TestCase =
  let make name parameters x =
    let meta = { Name = name; Parameters = parameters }
    TestCase(meta, fun () -> Done (meta, NonEmptyList.singleton x, TimeSpan.Zero))

  let makeError name parameters exn =
    let meta = { Name = name; Parameters = parameters }
    TestCase(meta, fun () -> Error (meta, [exn], [], TimeSpan.Zero))

  let addNotPassed notPassedCause (x: TestCase<_>) =
    TestCase(x.Metadata, fun () -> x.Run() |> TestResult.addAssertionResult (NotPassed notPassedCause))

  let combine (x: TestCaseType<'T>) (rest: 'T -> TestCase<'U>) =
    match x with
    | NoValueTest x ->
        TestCase(
          x.Metadata,
          fun () ->
            match x.Run() with
            | Done (meta, (Passed unit, []), duration) ->
                let watch = Stopwatch.StartNew()
                try (rest unit).Run() |> TestResult.addDuration duration
                with e ->
                  watch.Stop()
                  Error (meta, [e], [], duration + watch.Elapsed)
            | Done (meta, assertionResults, duration) ->
                // If the TestCase does not have any values,
                // even if the assertion is not passed,
                // the test is continuable.
                // So, continue the test.
                let notPassed =
                  assertionResults
                  |> NonEmptyList.toList
                  |> AssertionResult.List.onlyNotPassed
                let watch = Stopwatch.StartNew()
                try
                  match notPassed with
                  | [] -> failwith "oops!"
                  | head::tail ->
                      assert (typeof<'T> = typeof<unit>)
                      // continue the test!
                      let testRes = (rest Unchecked.defaultof<'T>).Run()
                      testRes
                      |> TestResult.addAssertionResults (NonEmptyList.make (NotPassed head) (tail |> List.map NotPassed))
                      |> TestResult.addDuration duration
                with e ->
                  watch.Stop()
                  Error (meta, [e], notPassed, duration + watch.Elapsed)
            | Error (meta, es, results, duration) ->
                // If the TestCase does not have any values,
                // even if the assertion is not passed,
                // the test is continuable.
                // So, continue the test.
                let watch = Stopwatch.StartNew()
                try
                  assert (typeof<'T> = typeof<unit>)
                  // continue th test!
                  let testRes = (rest Unchecked.defaultof<'T>).Run()
                  match results with
                  | [] -> testRes
                  | head::tail ->
                      testRes
                      |> TestResult.addAssertionResults (NonEmptyList.make (NotPassed head) (tail |> List.map NotPassed))
                      |> TestResult.addDuration duration
                with
                  e ->
                    watch.Stop()
                    Error (meta, e::es, results, duration + watch.Elapsed)
        )
    | HasValueTest x ->
        TestCase(
          x.Metadata,
          fun () ->
            match x.Run() with
            | Done (meta, (Passed value, []), duration) ->
                let watch = Stopwatch.StartNew()
                try (rest value).Run()
                with e ->
                  watch.Stop()
                  Error (meta, [e], [], duration + watch.Elapsed)
            | Done (meta, assertionResults, duration) ->
                // If the TestCase has some values,
                // the test is not continuable.
                let notPassed =
                  assertionResults
                  |> NonEmptyList.toList
                  |> AssertionResult.List.onlyNotPassed
                match notPassed with
                | [] -> failwith "oops!"
                | head::tail -> Done (meta, NonEmptyList.make (NotPassed head) (tail |> List.map NotPassed), duration)
            | Error (meta, es, results, duration) ->
                // If the TestCase has some values,
                // the test is not continuable.
                Error (meta, es, results, duration)
        )

type TestCaseWithBeforeOrAfter internal (testCase: TestCase<obj>) =
  inherit TestObject()
  member internal __.Body = testCase
  override __.SetNameIfNeed(newName: string) =
    let tc = TestCase<obj>({ testCase.Metadata with Name = if testCase.Metadata.Name = "" then newName else testCase.Metadata.Name }, testCase.Run)
    TestCaseWithBeforeOrAfter(tc) :> TestObject

type Action =
  | Empty
  | Before of (unit -> unit)
  | After of (unit -> unit)
  | BeforeAfter of (unit -> unit) * (unit -> unit)
