namespace Persimmon

open System
open System.Diagnostics
open System.Reflection
open System.Collections.Generic
open System.Threading
open Microsoft.FSharp.Control

#if PCL || NETSTANDARD
type MarshalByRefObject() = class end
#endif

/// The cause of not passed assertion.
type NotPassedCause =
    /// The assertion is not passed because it is skipped.
  | Skipped of string
    /// The assertion is not passed because it is violated.
  | Violated of string

/// The result of each assertion. (fake base type)
/// Can use active recognizers: Passed / NotPassed cause
type AssertionResult =
  abstract Value : obj option
  abstract LineNumber: int option
  abstract Status : NotPassedCause option
  abstract Box : unit -> AssertionResult<obj>

/// The result of each assertion.
and AssertionResult<'T> =
  inherit AssertionResult

/// The assertion is passed.
and [<Sealed>] Passed<'T>(value: 'T) =
  inherit MarshalByRefObject()

  member internal this.Value : 'T = value

  interface AssertionResult<'T> with
    member this.Value = Some (value :> obj)
    member this.LineNumber = None
    member this.Status = None
    member this.Box() = Passed(value :> obj) :> AssertionResult<obj>

/// The assertion is not passed.
and [<Sealed>] NotPassed<'T>(lineNumber: int option, cause: NotPassedCause) =
  inherit MarshalByRefObject()

  member internal this.LineNumber : int option = lineNumber
  member internal this.Cause : NotPassedCause = cause

  interface AssertionResult<'T> with
    member this.Value = None
    member this.LineNumber = lineNumber
    member this.Status = Some cause
    member this.Box() = NotPassed(lineNumber, cause) :> AssertionResult<obj>

[<AutoOpen>]
module AssertionResultExtensions =
  let (|Passed|NotPassed|) (result: AssertionResult<'T>) =
    match result with
    | :? Passed<'T> as p -> Passed (p.Value)
    | :? NotPassed<'T> as np -> NotPassed (np.LineNumber, np.Cause)
    | _ -> ArgumentException() |> raise

  let Passed (value: 'T) = Passed(value) :> AssertionResult<'T>
  let NotPassed (lineNumber: int option, cause: NotPassedCause) = NotPassed(lineNumber, cause) :> AssertionResult<'T>

/// NotPassedCause manipulators.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NotPassedCause =

  /// NotPassedCause via sequence manipulators.
  module Seq =

    let toAssertionResultList xs = xs |> Seq.map (fun x -> NotPassed(None, x))

    let skipMessages xs = xs |> Seq.choose (function Skipped msg -> Some msg | Violated _ -> None)
    let violatedMessages xs = xs |> Seq.choose (function Skipped _ -> None | Violated msg -> Some msg)

/// AssertionResult manipulators.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AssertionResult =

  let private selector (current: #AssertionResult) (next: #AssertionResult) =
    // Cannot use active recognizer, because forward reference.
    match current.Status, next.Status with
    | (Some (Violated _)), _ -> current
    | _, (Some (Violated _)) -> next
    | (Some (Skipped _)), _ -> current
    | _, (Some (Skipped _)) -> next
    | _, _ -> current

  /// AssertionResult via sequence manipulators.
  module Seq =

    let onlyNotPassed xs =
      xs |> Seq.choose (function NotPassed(_, x) -> Some x | _ -> None)

    /// Calculate the typical assertion result.
    /// It returns most important result.
    /// For example, "Violated" is more important than "Passed"
    let typicalResult (xs: #AssertionResult seq) = xs |> Seq.reduce selector

  /// AssertionResult via NonEmptyList manipulators.
  module NonEmptyList =
    /// Calculate the typical assertion result.
    /// It returns most important result.
    /// For example, "Violated" is more important than "Passed"
    let typicalResult (xs: NonEmptyList<#AssertionResult>) = xs |> NonEmptyList.reduce selector

/// Test metadata base class.
[<AbstractClass; StructuredFormatDisplay("{DisplayName}")>]
type TestMetadata internal(name: string option, categories: string seq) =
  inherit MarshalByRefObject()


  [<DefaultValue>]
  val mutable private symbolName : string option
  [<DefaultValue>]
  val mutable private parent : TestMetadata option
  [<DefaultValue>]
  val mutable private index : int option

  let categories = Seq.toArray categories

  /// The test name. It doesn't contain the parameters.
  /// If not set, fallback to raw symbol name.
  member __.Name = name

  /// Parent metadata. Storing by TestCollector.
  member this.Parent = this.parent

  /// Metadata unique name.
  /// If the test has parameters then the value contains them.
  abstract UniqueName : string

  /// Metadata display name.
  abstract DisplayName : string

  /// Index if metadata place into sequence.
  member this.Index = this.index

  /// Metadata categories.
  member this.Categories = this.InternalCategories |> Seq.toArray

  /// For internal use only.
  member internal this.RawSymbolName = this.symbolName

  /// Metadata symbol name.
  /// This naming contains parent context symbol names.
  member this.SymbolName =
    // Combine parent symbol name.
    match (name, this.symbolName, this.parent) with
    | (_, Some rsn, Some p) -> p.SymbolName + "." + rsn
    | (_, Some rsn, _) -> rsn
    | (Some n, _, Some p) -> p.SymbolName + "." + n
    | (Some n, _, _) -> n
    | (_, _, Some p) -> p.SymbolName
    | _ -> ""

  /// Metadata string.
  override this.ToString() = this.UniqueName

  /// For internal use only.
  static member internal SafeName (name: string option, unresolved: string) =
    match name with
    | Some name -> name
    | None -> unresolved

  /// For internal use only.
  member internal this.TrySetIndex(index: int) =
    match this.index with
    | None -> this.index <- Some index
    | _ -> ()

  /// For internal use only.
  member internal this.TrySetSymbolName(symbolName: string) =
    match this.symbolName with
    | None -> this.symbolName <- Some symbolName
    | _ -> ()

  /// For internal use only.
  member internal this.TrySetParent(parent: TestMetadata) =
    match this.parent with
    | None -> this.parent <- Some parent
    | _ -> ()

  /// For internal use only.
  member internal this.InternalCategories =
    seq {
      yield! categories
      match this.parent with
      | Some p -> yield! p.InternalCategories
      | None -> ()
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private TestMetadata =

  /// Recursive traverse display name (this --> parent).
  let rec traverseDisplayName (this: TestMetadata) =
    match this.Name, this.RawSymbolName with
    // First priority: this.Name
    | Some n, _ -> n
    // Second priority: this.RawSymbolName
    | _, Some rsn -> rsn
    // Both None
    | _ ->
      match this.Parent with
      // this is child: recursive.
      | Some p -> traverseDisplayName p
      // Root: unresolved.
      | None -> "[Unresolved]"

/// Test case base class.
[<AbstractClass>]
type TestCase internal (name: string option, categories: string seq, parameters: (Type * obj) seq) =
  inherit TestMetadata (name, categories)

  /// The test parameters.
  /// If the test has no parameters then the value is empty list.
  member __.Parameters = parameters |> Seq.toArray

  /// For internal use only.
  abstract member OnAsyncRun: unit -> Async<TestResult>

  /// For internal use only.
  abstract member Box: unit -> TestCase<obj>

  /// Execute this test case.
  member this.AsyncRun() = this.OnAsyncRun()

  /// Create unique name.
  member private this.CreateUniqueName baseName =
    let parameters = this.Parameters |> PrettyPrinter.printAll
    match this.Index, parameters.Length, this.Parameters.Length with
    // Not assigned index and parameters.
    | None, 0, 0 -> baseName
    // Assigned index and empty parameter strings (but may be assigned real parameters).
    // Print with index:
    | Some index, 0, _ -> sprintf "%s[%d]" baseName index
    // Others, print with parameter strings.
    | Some index, _, _ -> sprintf "%s(%s)[%d]" baseName parameters index
    | None, _, _ -> sprintf "%s(%s)" baseName parameters

  /// Metadata unique name.
  /// If the test has parameters then the value contains them.
  override this.UniqueName =
    this.SymbolName |> this.CreateUniqueName

  /// Metadata display name.
  override this.DisplayName =
    TestMetadata.traverseDisplayName this |> this.CreateUniqueName

/// Non generic view for test result. (fake base type)
/// Can use active recognizers: ContextResult cr / TestResult tr / EndMarker
and ResultNode =
  interface end

/// Non generic view for test result. (fake base type)
/// Inherited from ResultNode.
and TestResult =
  inherit ResultNode
  abstract TestCase: TestCase
  abstract IsError: bool
  abstract FailureMessages: string[]
  abstract SkipMessages: string[]
  abstract Exceptions: exn[]
  abstract Duration: TimeSpan
  abstract AssertionResults: AssertionResult[]
  abstract Box: unit -> TestResult<obj>

/// Test case class.
and
  [<Sealed>]
  TestCase<'T> internal (name: string option, categories: string seq, parameters: (Type * obj) seq, asyncBody: AsyncLazy<TestResult<'T>>) =
  inherit TestCase(name, categories, parameters)

  /// Test body is async operation.
  new(name: string option, categories: string seq, parameters: (Type * obj) seq, asyncBody: TestCase<'T> -> Async<TestResult<'T>>) as this =
    TestCase<'T>(name, categories, parameters, AsyncLazy<TestResult<'T>>(fun _ -> asyncBody(this)))

  member private this.InternalAsyncRun() = async {
    let watch = Stopwatch.StartNew()
    let! result = asyncBody.AsyncGetValue()
    watch.Stop()
    return
      match result with
      | :? Error<'T> as e -> Error(this, e.Exceptions, e.Causes, watch.Elapsed) :> TestResult<'T>
      | :? Done<'T> as d -> Done(this, d.Results, watch.Elapsed) :> TestResult<'T>
      | _ -> ArgumentException() |> raise
  }

  /// Execute this test case.
  member this.AsyncRun() = this.InternalAsyncRun()

  override this.OnAsyncRun() = async {
    let! result = this.InternalAsyncRun()
    return result :> TestResult
  }

  /// For internal use only.
  override this.Box() =
    let asyncBody tcobj = async {
      let! result = this.AsyncRun()
      return result.Box()
    }
    TestCase<obj>(this.Name, this.Categories, this.Parameters, asyncBody)

/// Test result type.
/// Inherited from ResultNode
and TestResult<'T> =
  inherit TestResult

/// This case represents the error.
and [<Sealed>] Error<'T>(testCase: TestCase, exns: exn [], causes: NotPassedCause list, duration: TimeSpan) =
  inherit MarshalByRefObject()

  member __.TestCase : TestCase = testCase
  member __.Exceptions : exn [] = exns
  member __.Causes : NotPassedCause list = causes
  member __.Duration : TimeSpan = duration

  member __.FailureMessages =
    causes
    |> NotPassedCause.Seq.violatedMessages
    |> Seq.toArray

  member __.SkipMessages =
    causes
    |> NotPassedCause.Seq.skipMessages
    |> Seq.toArray

  override __.ToString() = sprintf "%A: Result=Error" testCase

  interface TestResult<'T> with
    member __.TestCase = testCase
    member this.IsError = true
    member this.FailureMessages =
      this.FailureMessages
    member this.SkipMessages =
      this.SkipMessages
    member this.Exceptions = exns |> Seq.toArray
    member this.Duration = this.Duration
    member this.AssertionResults = [||]
    member this.Box() = Error(testCase, exns, causes, duration) :> TestResult<obj>

/// This case represents that all of the assertions is finished.
and [<Sealed>] Done<'T>(testCase: TestCase, results: NonEmptyList<AssertionResult<'T>>, duration: TimeSpan) =
  inherit MarshalByRefObject()

  member __.TestCase : TestCase = testCase
  member __.Results : NonEmptyList<AssertionResult<'T>> = results
  member __.Duration : TimeSpan = duration
  member __.Exceptions: exn [] = [||]

  member __.FailureMessages =
    results
    |> NonEmptyList.toSeq
    |> AssertionResult.Seq.onlyNotPassed
    |> NotPassedCause.Seq.violatedMessages
    |> Seq.toArray
  member __.SkipMessages =
    results
    |> NonEmptyList.toSeq
    |> AssertionResult.Seq.onlyNotPassed
    |> NotPassedCause.Seq.skipMessages
    |> Seq.toArray

  override __.ToString() = sprintf "%A: Result=Done" testCase

  interface TestResult<'T> with
    member __.TestCase = testCase
    member __.IsError = false
    member this.FailureMessages =
      this.FailureMessages
    member this.SkipMessages =
      this.SkipMessages
    member this.Exceptions = this.Exceptions
    member this.Duration = this.Duration
    member this.AssertionResults =
      results |> NonEmptyList.toList |> Seq.map (fun ar -> ar :> AssertionResult) |> Seq.toArray
    member this.Box() =
      Done (testCase, results |> NonEmptyList.toSeq |> Seq.map (fun ar -> ar.Box()) |> NonEmptyList.ofSeq, duration)
      :> TestResult<obj>

[<AutoOpen>]
module TestResultExtensions =
  let (|Error|Done|) (testResult: TestResult<'T>) =
    match testResult with
    | :? Error<'T> as e -> Error(e.TestCase, e.Exceptions, e.Causes, e.Duration)
    | :? Done<'T> as d -> Done(d.TestCase, d.Results, d.Duration)
    | _ -> ArgumentException() |> raise

  let Error (testCase: TestCase, exns: exn [], causes: NotPassedCause list, duration: TimeSpan) = Error(testCase, exns, causes, duration) :> TestResult<'T>
  let Done (testCase: TestCase, results: NonEmptyList<AssertionResult<'T>>, duration: TimeSpan) = Done(testCase, results, duration) :> TestResult<'T>

/// Test context class. (structuring nested test node)
[<Sealed>]
type Context =
  inherit TestMetadata

  [<DefaultValue>]
  val mutable private _children : TestMetadata[]

  /// Constructor.
  new (name: string, categories: string seq, children: TestMetadata seq) as this =
    { inherit TestMetadata(Some name, categories) } then
      this._children <- children |> Seq.toArray
      for child in this._children do child.TrySetParent(this :> TestMetadata)

  /// Construct unique name.
  member private this.CreateUniqueName baseName =
    match this.Index with
    // If index not assigned.
    | None -> baseName
    // Assigned index: print with index.
    | Some index -> sprintf "%s[%d]" baseName index

  /// Metadata unique name.
  /// If the test has parameters then the value contains them.
  override this.UniqueName =
    this.SymbolName |> this.CreateUniqueName

  /// Metadata display name.
  override this.DisplayName =
    TestMetadata.traverseDisplayName this |> this.CreateUniqueName

  /// Child tests.
  member this.Children = this._children

/// Test context and hold tested results class. (structuring nested test result node)
/// Inherited from ResultNode
[<Sealed>]
type ContextResult internal (context: Context, results: ResultNode[]) =
  inherit MarshalByRefObject()

  /// Target context.
  member __.Context = context

  /// Child results.
  member __.Results = results

  interface ResultNode

/// Set categories for tests that belong to the module or the type.
[<AttributeUsage(AttributeTargets.Class, AllowMultiple = true)>]
type CategoryAttribute([<ParamArray>] categories: string[]) =
  inherit Attribute()

  member this.Categories = categories