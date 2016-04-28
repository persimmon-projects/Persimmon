namespace Persimmon

open System
open System.Diagnostics
open System.Reflection
open System.Collections.Generic
open System.Threading

///////////////////////////////////////////////////////////////////////////

/// The cause of not passed assertion.
type NotPassedCause =
    /// The assertion is not passed because it is skipped.
  | Skipped of string
    /// The assertion is not passed because it is violated.
  | Violated of string

/// The result of each assertion. (fake base type)
/// Can use active recognizers: Passed / NotPassed cause
type AssertionResult =
  abstract Status : NotPassedCause option

/// The result of each assertion.
type AssertionResult<'T> =
    /// The assertion is passed.
  | Passed of 'T
    /// The assertion is not passed.
  | NotPassed of NotPassedCause

  interface AssertionResult with
    member this.Status =
      match this with
      | Passed _ -> None
      | NotPassed cause -> Some cause

/// NotPassedCause manipulators.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NotPassedCause =

  /// NotPassedCause via sequence manipulators.
  module Seq =

    let toAssertionResultList xs = xs |> Seq.map NotPassed

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
      xs |> Seq.choose (function NotPassed x -> Some x | _ -> None)

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

///////////////////////////////////////////////////////////////////////////

/// Test metadata base class.
[<AbstractClass>]
[<StructuredFormatDisplay("{UniqueName}")>]
type TestMetadata =

  val _name : string option
  [<DefaultValue>]
  val mutable _symbolName : string option
  [<DefaultValue>]
  val mutable _parent : TestMetadata option

  /// Constructor.
  internal new (name: string option) = {
    _name = name
  }

  /// The test name. It doesn't contain the parameters.
  /// If not set, fallback to raw symbol name.
  member this.Name = this._name

  /// Parent metadata. Storing by TestCollector.
  member this.Parent = this._parent

  /// Metadata unique name.
  /// If the test has parameters then the value contains them.
  abstract UniqueName : string

  /// Metadata display name.
  abstract DisplayName : string

  /// For internal use only.
  member internal this.RawSymbolName = this._symbolName

  /// Metadata symbol name.
  /// This naming contains parent context symbol names.
  member this.SymbolName =
    // Combine parent symbol name.
    match (this._name, this._symbolName, this._parent) with
    | (_, Some rsn, Some p) -> p.SymbolName + "." + rsn
    | (_, Some rsn, _) -> rsn
    | (Some n, _, Some p) -> p.SymbolName + "." + n
    | (Some n, _, _) -> n
    | (_, _, Some p) -> p.SymbolName
    | _ -> "[Unresolved]"

  /// Metadata string.
  override this.ToString() = this.UniqueName

  /// For internal use only.
  static member internal safeName (name: string option, unresolved: string) = 
    match name with
    | Some name -> name
    | None -> unresolved

  /// For internal use only.
  member internal this.trySetSymbolName(symbolName: string) =
    match this._symbolName with
    | None -> this._symbolName <- Some symbolName
    | _ -> ()

  /// For internal use only.
  member internal this.trySetParent(parent: TestMetadata) =
    match this._parent with
    | None -> this._parent <- Some parent
    | _ -> ()

//  interface ITestMetadata with
//    member this.Name = this.Name
//    member this.Parent = this.Parent
//    member this.SymbolName = this.SymbolName
//    member this.UniqueName = this.UniqueName
//    member this.DisplayName = this.DisplayName

///////////////////////////////////////////////////////////////////////////

/// Test case base class.
[<AbstractClass>]
type TestCase internal (name: string option, parameters: (Type * obj) seq) =
  inherit TestMetadata (name)

  /// Recursive traverse display name (this --> parent).
  let rec traverseDisplayName (this: TestMetadata) =
    match (this.Name, this.RawSymbolName) with
    // First priority: this.Name
    | (Some n, _) -> n
    // Second priority: this.RawSymbolName
    | (_, Some rsn) -> rsn
    // Both None
    | _ ->
      match this.Parent with
      // this is child: recursive.
      | Some p -> traverseDisplayName p
      // Root: unresolved.
      | None -> "[Unresolved]"

  /// The test parameters.
  /// If the test has no parameters then the value is empty list.
  member __.Parameters = parameters |> Seq.toArray
    
  /// For internal use only.
  abstract member OnAsyncRun: unit -> Async<TestResult>

  /// Execute this test case.
  member this.AsyncRun() = this.OnAsyncRun()
  
  /// Execute this test case.
  /// TODO: Omit all synch caller.
  //[<Obsolete>]
  member this.Run() = this.OnAsyncRun() |> Async.RunSynchronously
  
  member private this.createUniqueName baseName =
    match Array.isEmpty this.Parameters with
    | true -> baseName
    | false -> sprintf "%s(%s)" baseName (this.Parameters |> PrettyPrinter.printAll)

  /// Metadata unique name.
  /// If the test has parameters then the value contains them.
  override this.UniqueName =
    this.SymbolName |> this.createUniqueName

  /// Metadata display name.
  override this.DisplayName =
    traverseDisplayName this |> this.createUniqueName

//  interface ITestCase with
//    member this.Parameters = this.Parameters
//    member __.AsyncRun() = new InvalidOperationException() |> raise  // HACK

/// Non generic view for test result. (fake base type)
/// Can use active recognizers: ContextResult cr / TestResult tr / EndMarker
and ResultNode =
  interface end

/// Non generic view for test result. (fake base type)
/// Inherited from ResultNode.
and TestResult =
  inherit ResultNode
  abstract TestCase: TestCase
  abstract Exceptions: exn[]
  abstract Duration: TimeSpan
  abstract AssertionResults: AssertionResult[]

///////////////////////////////////////////////////////////////////////////

/// Test case class.
[<Sealed>]
type TestCase<'T> =
  inherit TestCase

  val _asyncBody : TestCase<'T> -> Async<TestResult<'T>>

  /// Constructor.
  /// Test body is async operation.
  new(name: string option, parameters: (Type * obj) seq, asyncBody: TestCase<'T> -> Async<TestResult<'T>>) = {
    inherit TestCase(name, parameters)
    _asyncBody = asyncBody
  }

  /// Constructor.
  /// Test body is synch operation.
  new(name: string option, parameters: (Type * obj) seq, body: TestCase<'T> -> TestResult<'T>) = {
    inherit TestCase(name, parameters)
    _asyncBody = fun tc -> async {
      return body(tc)
    }
  }

  /// Run test implementation core.
  member private this.InternalAsyncRun() = async {
    let watch = Stopwatch.StartNew()
    let! result = this._asyncBody(this)
    watch.Stop()
    return
      match result with
        | Error (_, errs, res, _) -> Error (this, errs, res, watch.Elapsed)
        | Done (_, res, _) -> Done (this, res, watch.Elapsed)
  }

  /// Execute this test case.
  member this.AsyncRun() = this.InternalAsyncRun()
    
  /// Execute this test case.
  /// TODO: Omit all synch caller.
  //[<Obsolete>]
  member this.Run() = this.InternalAsyncRun() |> Async.RunSynchronously

  /// For internal use only.
  override this.OnAsyncRun() = async {
    let! result = this.InternalAsyncRun()
    return result :> TestResult
  }

//  interface ITestCase with
//    override this.AsyncRun() = async {
//      let! result = this.InternalAsyncRun()
//      return result :> ITestResult
//    }

///////////////////////////////////////////////////////////////////////////

/// Test result union type.
/// Inherited from ResultNode
and TestResult<'T> =
  /// This case represents the error.
  | Error of TestCase * exn list * NotPassedCause list * TimeSpan
  /// This case represents that all of the assertions is finished.
  | Done of TestCase * NonEmptyList<AssertionResult<'T>> * TimeSpan
  with
    override this.ToString() =
      let result =
        match this with
        | Error (testCase, _, _, _) -> (testCase, "Error")
        | Done (testCase, _, _) -> (testCase, "Done")
      sprintf "%A: Result=%s" (result |> fst) (result |> snd)

    member this.TestCase =
      match this with
      | Error (testCase, _, _, _) -> testCase
      | Done (testCase, _, _) -> testCase
    member this.Exceptions =
      match this with
      | Error (_, exns, _, _) -> exns |> Seq.toArray
      | Done (_, _, _) -> [||]
    member this.Duration =
      match this with
      | Error (_, _, _, duration) -> duration
      | Done (_, _, duration) -> duration
    member this.AssertionResults =
      match this with
      | Error (_, _, _, _) -> [||]
      | Done (_, res, _) -> res |> NonEmptyList.toList |> Seq.map (fun ar -> ar :> AssertionResult) |> Seq.toArray

    interface TestResult with
      member this.TestCase = this.TestCase
      member this.Exceptions = this.Exceptions
      member this.Duration = this.Duration
      member this.AssertionResults = this.AssertionResults

//    interface ITestResult with
//      member this.TestCase = this.TestCase :> TestCase
//      member this.Exceptions = this.Exceptions
//      member this.Duration = this.Duration

///////////////////////////////////////////////////////////////////////////

/// Test context class. (structuring nested test node)
[<Sealed>]
type Context =
  inherit TestMetadata

  [<DefaultValue>]
  val mutable private _children : TestMetadata[]

  /// Constructor.
  new (name: string, children: TestMetadata seq) as this =
    { inherit TestMetadata(Some name) } then
      this._children <- children |> Seq.toArray
      for child in this._children do child.trySetParent(this :> TestMetadata)

  /// Metadata unique name.
  /// If the test has parameters then the value contains them.
  override this.UniqueName = this.SymbolName

  /// Metadata display name.
  override this.DisplayName = this.Name.Value

  /// Child tests.
  member this.Children = this._children

  override this.ToString() =
    sprintf "%s(%A)" this.SymbolName this._children

/// Test context and hold tested results class. (structuring nested test result node)
/// Inherited from ResultNode
[<Sealed>]
type ContextResult internal (context: Context, results: ResultNode[]) =

  /// Target context.
  member __.Context = context

  /// Child results.
  member __.Results = results

  interface ResultNode
