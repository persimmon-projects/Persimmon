namespace Persimmon

open System
open System.Diagnostics
open System.Reflection

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

///////////////////////////////////////////////////////////////////////////
// Test metadata base class

[<AbstractClass>]
[<StructuredFormatDisplay("{UniqueName}")>]
type TestMetadata internal (name: string) =

  let mutable _parent : TestMetadata option = None

  /// The test name. It doesn't contain the parameters.
  member __.Name = name

  /// Parent metadata. Storing by TestCollector.
  member __.Parent = _parent

  /// Metadata symbol name.
  member this.SymbolName =
    match _parent with
    | Some parent -> parent.SymbolName + "." + name
    | None -> name

  /// The test unique name (if the test has parameters then the value contains them).
  member this.UniqueName = this.ToString()

  /// Metadata string.
  override this.ToString() = this.SymbolName

  /// Execute the test.
  abstract member Run : unit -> unit

  /// For internal use only.
  member internal this.SetParent(parent: TestMetadata) =
    _parent <- Some parent

///////////////////////////////////////////////////////////////////////////
// Test result class

[<AbstractClass>]
type TestResult internal (testMetadata: TestMetadata) =
  /// Target test metadata.
  member __.Metadata = testMetadata

[<Sealed>]
type TestResult<'T> (testMetadata: TestMetadata) =
  inherit TestResult(testMetadata)
  

///////////////////////////////////////////////////////////////////////////
// Test case class

[<AbstractClass>]
type TestCase internal (name: string, parameters: (Type * obj) list) =
  inherit TestMetadata (name)

  /// The test parameters.
  /// If the test has no parameters then the value is empty list.
  member __.Parameters = parameters
    
  /// Metadata string.
  override this.ToString() =
    match this.Parameters.IsEmpty with
    | true -> sprintf "%s(%s)" this.Name (this.Parameters |> PrettyPrinter.printAll)
    | false -> this.Name

[<Sealed>]
type TestCase<'T> (name: string, parameters: (Type * obj) list, body: Lazy<TestResult<'T>>) =
  inherit TestCase (name, parameters)

  /// Execute the test.
  override __.Run() =
    let watch = Stopwatch.StartNew()
    let result = body.Value
    watch.Stop()
    match result with
    | Error (_, errs, res, _) -> Error (metadata, errs, res, watch.Elapsed)
    | Done (_, res, _) -> Done (metadata, res, watch.Elapsed)

///////////////////////////////////////////////////////////////////////////
// Test context class (structuring nested test node)

[<Sealed>]
type Context (name: string, children: TestMetadata list) =
  inherit TestMetadata (name)
    
  /// Metadata string.
  override this.ToString() =
    match this.Parameters.IsEmpty with
    | true -> sprintf "%s(%s)" this.Name (this.Parameters |> PrettyPrinter.printAll)
    | false -> this.Name






/// This class represents a nested test result.
/// After running tests, the Context objects become the ContextReults objects.
type ContextResult internal (name: string, testMetadata: TestMetadata, children: ITestResultNode list) =

  member __.Name = name
  member __.SymbolName = testMetadata.SymbolName
  member __.Children = children

  override this.ToString() = sprintf "%A" this

  interface ITestResultNode with
    member this.Name = this.Name
    member this.SymbolName = this.SymbolName
    
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
    /// Metadata symbol name
    member this.SymbolName = this.Metadata.SymbolName

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

    member this.Exceptions =
      match this with
      | Error (_, exns, _, _) -> exns |> Seq.toArray
      | Done (_, _, _) -> [||]

    member this.Duration =
      match this with
      | Error (_, _, _, duration) -> duration
      | Done (_, _, duration) -> duration

    interface ITestResult with
      member this.Name = this.Name
      member this.SymbolName = this.SymbolName
      member this.FullName = this.FullName
      member this.Parameters = this.Parameters
      member this.Exceptions = this.Exceptions
      member this.Duration = this.Duration

/// This class represents a nested test.
/// We can use this class for grouping of the tests.
type Context private (name: string, testMetadata: TestMetadata option, children: ITestObject list) =
  inherit TestObject ()

  new(name: string, children: ITestObject seq) = Context(name, None, children |> Seq.toList)
  new(name: string, testMetadata: TestMetadata, children: ITestObject seq) = Context(name, Some testMetadata, children |> Seq.toList)

  /// The context name.
  override __.Name = Some name
  /// Metadata symbol name
  override __.SymbolName =
    match testMetadata with
    | Some tm -> tm.SymbolName
    | None -> "(Unknown)"

  /// (For internal use only)
  member __.CreateAdditionalMetadataIfNeed(newName: string, newTestMetadata: TestMetadata, mapper: (ITestObject -> ITestObject) option) =
    Context(
      (if name = "" then newName else name),
      (match testMetadata with
       | Some _ -> testMetadata
       | None -> Some newTestMetadata),
      (match mapper with
       | Some m -> children |> Seq.map m |> Seq.toList
       | None -> children))

  /// This is a list that has the elements represented the subcontext or the test case.
  member __.Children = children

  /// Execute tests recursively.
  member __.Run(reporter: ITestResultNode -> unit) =
    ContextResult(name, testMetadata.Value, children
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

  member __.CreateAdditionalMetadataIfNeed(newName: string, newDeclaredMember: MemberInfo) =
    TestCase<'T>({ Name =
                     match metadata.Name with
                     | None -> Some newName
                     | _ -> metadata.Name;
                   DeclaredMember =
                     match metadata.DeclaredMember with
                     | None -> Some newDeclaredMember
                     | _ -> metadata.DeclaredMember;
                   Parameters = metadata.Parameters
                 },
                 body)

  member internal __.Metadata = metadata

  /// The test name. It doesn't contain the parameters.
  override __.Name = metadata.Name
    /// The test defined member. Storing by TestCollector.
  override __.DeclaredMember = metadata.DeclaredMember
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
    member this.CreateAdditionalMetadataIfNeed(newName: string, newDeclaredMember: MemberInfo) =
      this.CreateAdditionalMetadataIfNeed(newName, newDeclaredMember) :> ITestCase
    member this.Run() = this.Run() :> ITestResult

// extension
type TestCase<'T> with
  /// Convert TestCase<'T> to TestCase<obj>.
  member this.BoxTypeParam() =
    TestCase<obj>(this.Metadata, fun () -> this.Run().BoxTypeParam())
