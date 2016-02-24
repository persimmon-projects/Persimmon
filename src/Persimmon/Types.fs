namespace Persimmon

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
[<StructuredFormatDisplay("{FullName}")>]
type TestMetadata(name: string option, parameters: (Type * obj) list) =

  /// The test name. It doesn't contain the parameters.
  member __.Name = name
  /// The test name(if the test has parameters then the value contains them).
  member this.FullName = this.ToString()
  /// The test parameters.
  /// If the test has no parameters then the value is empty list.
  member __.Parameters = parameters

  override this.ToString() =
    match this.Name with
    | Some name when this.Parameters.IsEmpty -> name
    | Some name ->
      sprintf "%s(%s)" name (this.Parameters |> PrettyPrinter.printAll)
    | None -> ""

  interface ITestCaseNode with
    member this.Name = this.Name
    member this.FullName = this.FullName
    member this.Parameters = this.Parameters

/// The type that is treated as tests by Persimmon.
/// Derived class of this class are only two classes,
/// they are Context and TestCase<'T>.
/// When the TestObject is executed becomes TestResult.
/// You should use the ActivePatterns
/// if you want to process derived objects through this class.
[<AbstractClass>]
type TestObject internal () =

  abstract member Name: string option
  abstract member SetNameIfNeed: string -> TestObject

  interface ITestObject with
    member this.Name = this.Name
    member this.SetNameIfNeed(newName: string) = this.SetNameIfNeed(newName) :> ITestObject
    
/// This class represents a nested test result.
/// After running tests, the Context objects become the ContextReults objects.
type ContextResult(name: string, children: ITestResult list) =
  member __.Name = name
  member __.Children = children
  override this.ToString() = sprintf "%A" this
  interface ITestResult with
    member this.Name = Some this.Name

/// This class represents a nested test.
/// We can use this class for grouping of the tests.
type Context(name: string, children: ITestObject seq) =
  inherit TestObject ()

  /// The context name.
  override __.Name = Some name

  override __.SetNameIfNeed(newName: string) =
    Context((if name = "" then newName else name), children) :> TestObject

  /// This is a list that has the elements represented the subcontext or the test case.
  member __.Children = children

  /// Execute tests recursively.
  member __.Run(reporter: ITestResult -> unit) =
    ContextResult(name, children
      |> Seq.map (function
        | :? Context as c ->
            let res = c.Run(reporter) :> ITestResult
            reporter res
            res
        | x (* :? TestCase<_> *) ->
            let run = x.GetType().GetMethod("Run")
            let res = run.Invoke(x, [||]) :?> ITestResult
            reporter res
            res)
      |> Seq.toList)    // "Run" is meaing run tests and fixed results at this point.

  override __.ToString() =
    sprintf "Context(%A, %A)" name children
    
/// The result of each test.
/// After running tests, the TestCase objects become the TestResult objects.
type TestResult<'T> =
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

    interface ITestResult with
      member this.Name = this.Name

/// This class represents a test that has not been run yet.
/// In order to run the test represented this class, use the "Run" method.
type TestCase<'T>(metadata: TestMetadata, body: unit -> TestResult<'T>) =
  inherit TestObject ()

  new (name, parameters, body) = TestCase<_>(TestMetadata(name, parameters), body)

  override __.SetNameIfNeed(newName: string) =
    TestCase<'T>(
      TestMetadata(
        (match metadata.Name with None -> Some newName | _ -> metadata.Name),
        metadata.Parameters),
      body) :> TestObject

  member internal __.Metadata = metadata

  /// The test name. It doesn't contain the parameters.
  override __.Name = metadata.Name
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
    | Error (_, errs, res, _) -> Error (metadata, errs, res, watch.Elapsed)
    | Done (_, res, _) -> Done (metadata, res, watch.Elapsed)

  override __.ToString() =
    sprintf "TestCase<%s>(%A)" (typeof<'T>.Name) metadata

  interface ITestCase with
    member this.FullName = this.FullName
    member this.Parameters = this.Parameters
    member this.Run() = this.Run() :> ITestResult

// extension
type TestCase<'T> with
  /// Convert TestCase<'T> to TestCase<obj>.
  member this.BoxTypeParam() =
    TestCase<obj>(this.Metadata, fun () -> this.Run().BoxTypeParam())
