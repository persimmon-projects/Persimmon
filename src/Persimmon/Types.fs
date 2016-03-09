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
type TestMetadata = {
  /// The test name. It doesn't contain the parameters.
  Name: string option
  /// The test defined type. Storing by TestCollector.
  DeclaredType: Type option
  /// The test parameters.
  /// If the test has no parameters then the value is empty list.
  Parameters: (Type * obj) list
}
with
  /// The test name(if the test has parameters then the value contains them).
  member this.FullName = this.ToString()

  override this.ToString() =
    match this.Name with
    | Some name when this.Parameters.IsEmpty -> name
    | Some name ->
      sprintf "%s(%s)" name (this.Parameters |> PrettyPrinter.printAll)
    | None -> ""

  interface ITestMetadata with
    member this.Name = this.Name
    member this.DeclaredType = this.DeclaredType
    member this.FullName = this.FullName
    member this.Parameters = this.Parameters

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TestMetadata =
  // Construct TestMetadata
  let init name parameters =
    { Name = name; DeclaredType = None; Parameters = parameters }

/// The type that is treated as tests by Persimmon.
/// Derived class of this class are only two classes,
/// they are Context and TestCase<'T>.
/// When the TestObject is executed becomes TestResult.
/// You should use the ActivePatterns
/// if you want to process derived objects through this class.
[<AbstractClass>]
type TestObject internal () =

  /// The test name. It doesn't contain the parameters.
  abstract member Name: string option
  /// The test defined type. Storing by TestCollector.
  abstract member DeclaredType: Type option

  interface ITestObject with
    member this.Name = this.Name
    member this.DeclaredType = this.DeclaredType
    
/// This class represents a nested test result.
/// After running tests, the Context objects become the ContextReults objects.
type ContextResult internal (name: string, declaredType: Type, children: ITestResultNode list) =

  member __.Name = name
  member __.DeclaredType = declaredType
  member __.Children = children

  override this.ToString() = sprintf "%A" this

  interface ITestResultNode with
    member __.Name = name
    member __.DeclaredType = declaredType
    
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
    member this.Name = this.Metadata.Name.Value
    /// The context defined type. Storing by TestCollector.
    member this.DeclaredType = this.Metadata.DeclaredType.Value
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

    member this.Outcome = // TODO: lack of informations (ex: exn in failed)
      match this with
      | Error (_, _, _, _) -> "Failed"
      | Done (_, _, _) -> "Passed"

    interface ITestResult with
      member this.Name = this.Name
      member this.DeclaredType = this.DeclaredType
      member this.FullName = this.FullName
      member this.Parameters = this.Parameters
      member this.Outcome = // TODO: lack of informations (ex: exn in failed)
        match this with
        | Error (_, _, _, _) -> "Failed"
        | Done (_, _, _) -> "Passed"

/// This class represents a nested test.
/// We can use this class for grouping of the tests.
type Context private (name: string, declaredType: Type option, children: ITestObject list) =
  inherit TestObject ()

  new(name: string, children: ITestObject seq) = Context(name, None, children |> Seq.toList)
  new(name: string, declaredType: Type, children: ITestObject seq) = Context(name, Some declaredType, children |> Seq.toList)

  /// The context name.
  override __.Name = Some name
  /// The context defined type. Storing by TestCollector.
  override __.DeclaredType = declaredType
  /// (For internal use only)
  member __.CreateAdditionalMetadataIfNeed(newName: string, newDeclaredType: Type, mapper: (ITestObject -> ITestObject) option) =
    Context(
      (if name = "" then newName else name),
      (match declaredType with
       | Some _ -> declaredType
       | None -> Some newDeclaredType),
      (match mapper with
       | Some m -> children |> Seq.map m |> Seq.toList
       | None -> children)) :> TestObject

  /// This is a list that has the elements represented the subcontext or the test case.
  member __.Children = children

  /// Execute tests recursively.
  member __.Run(reporter: ITestResultNode -> unit) =
    ContextResult(name, declaredType.Value, children
      |> Seq.map (function
        | :? Context as c ->
            let res = c.Run(reporter) :> ITestResultNode
            reporter res
            res
        | x (* :? TestCase<_> *) ->
            let run = x.GetType().GetMethod("Run")
            let res = run.Invoke(x, [||]) :?> ITestResultNode
            reporter res
            res)
      |> Seq.toList  // "Run" is meaing JUST RUN tests and fixed results.
    )

  override __.ToString() =
    sprintf "Context(%A, %A)" name children

/// This class represents a test that has not been run yet.
/// In order to run the test represented this class, use the "Run" method.
type TestCase<'T>(metadata: TestMetadata, body: Lazy<TestResult<'T>>) =
  inherit TestObject ()

  new (metadata, body) = TestCase<_>(metadata, lazy body ())
  new (name, parameters, body: Lazy<_>) = TestCase<_>(TestMetadata.init name parameters, body)
  new (name, parameters, body) = TestCase<_>(TestMetadata.init name parameters, lazy body ())

  member __.CreateAdditionalMetadataIfNeed(newName: string, newDeclaredType: Type) =
    TestCase<'T>({ Name =
                     match metadata.Name with
                     | None -> Some newName
                     | _ -> metadata.Name;
                   DeclaredType =
                     match metadata.DeclaredType with
                     | None -> Some newDeclaredType
                     | _ -> metadata.DeclaredType;
                   Parameters = metadata.Parameters
                 },
                 body)

  member internal __.Metadata = metadata

  /// The test name. It doesn't contain the parameters.
  override __.Name = metadata.Name
  /// The context defined type. Storing by TestCollector.
  override __.DeclaredType = metadata.DeclaredType
  /// The test name(if the test has parameters then the value contains them).
  member __.FullName = metadata.FullName
  /// The test parameters.
  /// If the test has no parameters then the value is empty list.
  member __.Parameters = metadata.Parameters

  /// Execute the test.
  member __.Run() =
    let watch = Stopwatch.StartNew()
    let result = body.Value
    watch.Stop()
    match result with
    | Error (_, errs, res, _) -> Error (metadata, errs, res, watch.Elapsed)
    | Done (_, res, _) -> Done (metadata, res, watch.Elapsed)

  override __.ToString() =
    sprintf "TestCase<%s>(%A)" (typeof<'T>.Name) metadata

  interface ITestCase with
    member this.FullName = this.FullName
    member this.Parameters = this.Parameters
    member this.CreateAdditionalMetadataIfNeed(newName: string, newDeclaredType: Type) =
      this.CreateAdditionalMetadataIfNeed(newName, newDeclaredType) :> ITestCase
    member this.Run() = this.Run() :> ITestResult

// extension
type TestCase<'T> with
  /// Convert TestCase<'T> to TestCase<obj>.
  member this.BoxTypeParam() =
    TestCase<obj>(this.Metadata, fun () -> this.Run().BoxTypeParam())
