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
    new TestCase<_>(name, parameters, asyncBody)

  /// Create test case manually.
  /// TODO: Omit all synch caller.
  //[<Obsolete>]
  let initForSynch name parameters (body: TestCase<_> -> TestResult<_>) =
    new TestCase<_>(name, parameters, body)

  /// Create always completion test case.
  let makeDone name parameters x =
    new TestCase<_>(name, parameters, fun testCase -> Done (testCase, NonEmptyList.singleton x, TimeSpan.Zero))

  /// Create always error test case.
  let makeError name parameters exn =
    new TestCase<_>(name, parameters, fun testCase -> Error (testCase, [exn], [], TimeSpan.Zero))

//  let box (testCase: #TestCase) =
//    let asyncBody (tcobj: TestCase<obj>) = async {
//      let! result = testCase.AsyncRun()
//      return
//        match result with
//        | Error (_, errs, res, _) -> Error (this, errs, res, watch.Elapsed)
//        | Done (_, res, _) -> Done (this, res, watch.Elapsed)
//    }
//    new TestCase<obj>(testCase.Name, testCase.Parameters, asyncBody)

  /// Add not passed test after test.
  let addNotPassed notPassedCause (x: TestCase<_>) =
    new TestCase<_>(x.Name, x.Parameters, fun _ -> x.Run() |> TestResult.addAssertionResult (NotPassed notPassedCause))

  let private runNoValueTest (x: TestCase<'T>) (rest: 'T -> TestCase<'U>) =
    match x.Run() with
    | Done (testCase, (Passed unit, []), duration) ->
      let watch = Stopwatch.StartNew()
      try
        try (rest unit).Run() |> TestResult.addDuration duration
        finally watch.Stop()
      with e ->
        watch.Stop()
        Error (testCase, [e], [], duration + watch.Elapsed)
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
        | [] -> failwith "oops!"
        | head::tail ->
          assert (typeof<'T> = typeof<unit>)
          // continue the test!
          let testRes = (rest Unchecked.defaultof<'T>).Run()
          watch.Stop()
          testRes
          |> TestResult.addAssertionResults (NonEmptyList.make (NotPassed head) (tail |> List.map NotPassed))
          |> TestResult.addDuration duration
      with e ->
        watch.Stop()
        Error (testCase, [e], notPassed, duration + watch.Elapsed)
    | Error (testCase, es, results, duration) ->
      // If the TestCase does not have any values,
      // even if the assertion is not passed,
      // the test is continuable.
      // So, continue the test.
      let watch = Stopwatch.StartNew()
      try
        assert (typeof<'T> = typeof<unit>)
        // continue th test!
        let testRes = (rest Unchecked.defaultof<'T>).Run()
        watch.Stop()
        match results with
        | [] -> testRes |> TestResult.addExceptions es
        | head::tail ->
          testRes
          |> TestResult.addAssertionResults (NonEmptyList.make (NotPassed head) (tail |> List.map NotPassed))
          |> TestResult.addDuration duration
          |> TestResult.addExceptions es
      with e ->
        watch.Stop()
        Error (testCase, e::es, results, duration + watch.Elapsed)

  let private runHasValueTest (x: TestCase<'T>) (rest: 'T -> TestCase<'U>) =
    match x.Run() with
    | Done (testCase, (Passed value, []), duration) ->
      let watch = Stopwatch.StartNew()
      try
        let result = (rest value).Run()
        watch.Stop()
        result
      with e ->
        watch.Stop()
        Error (testCase, [e], [], duration + watch.Elapsed)
    | Done (testCase, assertionResults, duration) ->
      // If the TestCase has some values,
      // the test is not continuable.
      let notPassed =
        assertionResults
        |> NonEmptyList.toSeq |> AssertionResult.Seq.onlyNotPassed |> Seq.toList
      match notPassed with
      | [] -> failwith "oops!"
      | head::tail -> Done (testCase, NonEmptyList.make (NotPassed head) (tail |> List.map NotPassed), duration)
    | Error (testCase, es, results, duration) ->
      // If the TestCase has some values,
      // the test is not continuable.
      Error (testCase, es, results, duration)

  /// Combine tests.
  let combine (x: TestCaseType<'T>) (rest: 'T -> TestCase<'U>) =
    match x with
    | NoValueTest x ->
      TestCase<'U>(x.Name, x.Parameters, fun _ -> runNoValueTest x rest)
    | HasValueTest x ->
      TestCase<'U>(x.Name, x.Parameters, fun _ -> runHasValueTest x rest)
