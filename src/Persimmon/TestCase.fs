namespace Persimmon

open System
open System.Diagnostics

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

/// Test case manipulators.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TestCase =

  /// Create test case manually.
  let init name parameters (asyncBody: TestCase<_> -> Async<TestResult<_>>) =
    TestCase<_>(name, parameters, asyncBody)

  /// Create always completion test case.
  let makeDone name parameters x =
    TestCase<_>(name, parameters, fun testCase -> async { return Done (testCase, NonEmptyList.singleton x, TimeSpan.Zero) })

  /// Create always error test case.
  let makeError name parameters exn =
    TestCase<_>(name, parameters, fun testCase -> async { return Error (testCase, [exn], [], TimeSpan.Zero) })

  /// Add not passed test after test.
  let addNotPassed line notPassedCause (x: TestCase<_>) =
    TestCase<_>(
      x.Name,
      x.Parameters,
      fun _ -> async {
        let! result = x.AsyncRun()
        return TestResult.addAssertionResult (NotPassed(line, notPassedCause)) result
      }
    )

  let private runNoValueTest (x: TestCase<'T>) (rest: 'T -> TestCase<'U>) = async {
    let! result = x.AsyncRun()
    match result with
    | Done (testCase, (Passed unit, []), duration) ->
      let watch = Stopwatch.StartNew()
      try
        try
          let! result = (rest unit).AsyncRun()
          return TestResult.addDuration duration result
        finally watch.Stop()
      with e ->
        watch.Stop()
        return Error (testCase, [e], [], duration + watch.Elapsed)
    | Done (testCase, assertionResults, duration) ->
      // If the TestCase does not have any values,
      // even if the assertion is not passed,
      // the test is continuable.
      // So, continue the test.
      let notPassed =
        assertionResults
        |> NonEmptyList.toSeq |> AssertionResult.Seq.onlyNotPassed |> Seq.toList
      let watch = Stopwatch.StartNew()
      try
        match notPassed with
        | [] -> return failwith "oops!"
        | head::tail ->
          assert (typeof<'T> = typeof<unit>)
          // continue the test!
          let! testRes = (rest Unchecked.defaultof<'T>).AsyncRun()
          watch.Stop()
          return
            testRes
            |> TestResult.addAssertionResults (NonEmptyList.make (NotPassed(None, head)) (tail |> List.map (fun x -> NotPassed(None, x))))
            |> TestResult.addDuration duration
      with e ->
        watch.Stop()
        return Error (testCase, [e], notPassed, duration + watch.Elapsed)
    | Error (testCase, es, results, duration) ->
      // If the TestCase does not have any values,
      // even if the assertion is not passed,
      // the test is continuable.
      // So, continue the test.
      let watch = Stopwatch.StartNew()
      try
        assert (typeof<'T> = typeof<unit>)
        // continue th test!
        let! testRes = (rest Unchecked.defaultof<'T>).AsyncRun()
        watch.Stop()
        return
          match results with
          | [] -> testRes |> TestResult.addExceptions es
          | head::tail ->
            testRes
            |> TestResult.addAssertionResults (NonEmptyList.make (NotPassed(None, head)) (tail |> List.map (fun x -> NotPassed(None, x))))
            |> TestResult.addDuration duration
           |> TestResult.addExceptions es
      with e ->
        watch.Stop()
        return Error (testCase, e::es, results, duration + watch.Elapsed)
  }

  let private runHasValueTest (x: TestCase<'T>) (rest: 'T -> TestCase<'U>) = async {
    let! result = x.AsyncRun()
    match result with
    | Done (testCase, (Passed value, []), duration) ->
      let watch = Stopwatch.StartNew()
      try
        let! result = (rest value).AsyncRun()
        watch.Stop()
        return result
      with e ->
        watch.Stop()
        return Error (testCase, [e], [], duration + watch.Elapsed)
    | Done (testCase, assertionResults, duration) ->
      // If the TestCase has some values,
      // the test is not continuable.
      let notPassed =
        assertionResults
        |> NonEmptyList.toSeq |> AssertionResult.Seq.onlyNotPassed |> Seq.toList
      return
        match notPassed with
        | [] -> failwith "oops!"
        | head::tail -> Done (testCase, NonEmptyList.make (NotPassed(None, head)) (tail |> List.map (fun x -> NotPassed(None, x))), duration)
    | Error (testCase, es, results, duration) ->
      // If the TestCase has some values,
      // the test is not continuable.
      return Error (testCase, es, results, duration)
  }

  /// Combine tests.
  let combine (x: TestCaseType<'T>) (rest: 'T -> TestCase<'U>) =
    match x with
    | NoValueTest x ->
      TestCase<'U>(x.Name, x.Parameters, fun _ -> runNoValueTest x rest)
    | HasValueTest x ->
      TestCase<'U>(x.Name, x.Parameters, fun _ -> runHasValueTest x rest)
