namespace Persimmon

type NotPassedCause =
  | Skipped of string
  | Violated of string

/// The result of each assertion.
type AssertionResult<'T> =
  | Passed of 'T
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
[<AbstractClass>]
type TestObject internal () = class end

type ITestResult = interface end

/// This class represents a nested test.
/// We can use this class for grouping of the tests.
type Context(name: string, children: TestObject list) =
  inherit TestObject ()

  /// The context name.
  member __.Name = name
  /// This is a list that has the elements represented the subcontext or the test case.
  member __.Children = children

  /// Execute tests.
  member __.Run(reporter: ITestResult -> unit) =
    { Name = name
      Children =
        children
        |> Seq.map (function
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
  Children: ITestResult seq
}
with
  override this.ToString() = sprintf "%A" this
  interface ITestResult

/// This class represents a test that has not been run yet.
/// In order to run the test represented this class, use the "Run" method.
type TestCase<'T>(metadata: TestMetadata, body: unit -> TestResult<'T>) =
  inherit TestObject ()

  new (name, parameters, body) = TestCase<_>({ Name = name; Parameters = parameters }, body)

  member internal __.Metadata = metadata

  /// The test name. It doesn't contain the parameters.
  member __.Name = metadata.Name
  /// The test name(if the test has parameters then the value contains them).
  member __.FullName = metadata.FullName
  /// The test parameters.
  /// If the test has no parameters then the value is empty list.
  member __.Parameters = metadata.Parameters
  /// Execute the test.
  member __.Run() = body ()

  override __.ToString() =
    sprintf "TestCase<%s>(%A)" (typeof<'T>.Name) metadata

/// The result of each test.
/// After running tests, the TestCase objects become the TestResult objects.
and TestResult<'T> =
    /// This case represents the break by exception.
    /// If exist some assertion results before thrown exception, this case holds them.
  | Break of TestMetadata * exn * NotPassedCause list
    /// This case represents that all of the assertions is finished.
  | Done of TestMetadata * NonEmptyList<AssertionResult<'T>>
  with
    member private this.Metadata =
      match this with Break (x, _, _) | Done (x, _) -> x
    /// The test name. It doesn't contain the parameters.
    member this.Name = this.Metadata.Name
    /// The test name(if the test has parameters then the value contains them).
    member this.FullName = this.Metadata.FullName
    /// The test parameters.
    /// If the test has no parameters then the value is empty list.
    member this.Parameters = this.Metadata.Parameters

    member this.BoxTypeParam() =
      match this with
      | Break (meta, e, res) -> Break (meta, e, res)
      | Done (meta, res) ->
          Done (meta, res |> NonEmptyList.map (function Passed x -> Passed (box x) | NotPassed x -> NotPassed x))

    interface ITestResult

type TestCase<'T> with
  member this.BoxTypeParam() =
    TestCase<obj>(this.Metadata, fun () -> this.Run().BoxTypeParam())

module TestResult =
  let addAssertionResult x = function
  | Done (metadata, (Passed _, [])) -> Done (metadata, NonEmptyList.singleton x)
  | Done (metadata, results) -> Done (metadata, NonEmptyList.cons x results)
  | Break (metadata, e, results) -> Break (metadata, e, match x with Passed _ -> results | NotPassed x -> x::results)

  let addAssertionResults (xs: NonEmptyList<AssertionResult<_>>) = function
  | Done (metadata, (Passed _, [])) -> Done (metadata, xs)
  | Done (metadata, results) ->
      Done (metadata, NonEmptyList.appendList xs (results |> NonEmptyList.toList |> AssertionResult.List.onlyNotPassed |> NotPassedCause.List.toAssertionResultList))
  | Break (metadata, e, results) ->
      Break (metadata, e, (xs |> NonEmptyList.toList |> AssertionResult.List.onlyNotPassed)@results)

type TestType<'T> =
  | NoValueTest of TestCase<'T>
  | HasValueTest of TestCase<'T>

module TestCase =
  let make name parameters x =
    let meta = { Name = name; Parameters = parameters }
    TestCase(meta, fun () -> Done (meta, NonEmptyList.singleton x))

  let makeBreak name parameters exn =
    let meta = { Name = name; Parameters = parameters }
    TestCase(meta, fun () -> Break (meta, exn, []))

  let addNotPassed notPassedCause (x: TestCase<_>) =
    TestCase(x.Metadata, fun () -> x.Run() |> TestResult.addAssertionResult (NotPassed notPassedCause))

  let combine (x: TestType<'T>) (rest: 'T -> TestCase<'U>) =
    match x with
    | NoValueTest x ->
        TestCase(
          x.Metadata,
          fun () ->
            match x.Run() with
            | Done (meta, (Passed unit, [])) ->
                try (rest unit).Run()
                with e -> Break (meta, e, [])
            | Done (meta, assertionResults) ->
                let notPassed =
                  assertionResults
                  |> NonEmptyList.toList
                  |> AssertionResult.List.onlyNotPassed
                try
                  match notPassed with
                  | [] -> failwith "oops!"
                  | head::tail ->
                      assert (typeof<'T> = typeof<unit>)
                      let testRes = (rest Unchecked.defaultof<'T>).Run()
                      testRes |> TestResult.addAssertionResults (NonEmptyList.make (NotPassed head) (tail |> List.map NotPassed))
                with e -> Break (meta, e, notPassed)
            | Break (meta, e, results) ->
                Break (meta, e, results)
        )
    | HasValueTest x ->
        TestCase(
          x.Metadata,
          fun () ->
            match x.Run() with
            | Done (meta, (Passed value, [])) ->
                try (rest value).Run()
                with e -> Break (meta, e, [])
            | Done (meta, assertionResults) ->
                let notPassed =
                  assertionResults
                  |> NonEmptyList.toList
                  |> AssertionResult.List.onlyNotPassed
                match notPassed with
                | [] -> failwith "oops!"
                | head::tail -> Done (meta, NonEmptyList.make (NotPassed head) (tail |> List.map NotPassed))
            | Break (meta, e, results) ->
                Break (meta, e, results)
        )