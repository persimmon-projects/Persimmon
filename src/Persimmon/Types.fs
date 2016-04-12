namespace Persimmon

open System
open System.Diagnostics
open System.Reflection

///////////////////////////////////////////////////////////////////////////

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

/// Test metadata base class.
[<AbstractClass>]
[<StructuredFormatDisplay("{UniqueName}")>]
type TestMetadata internal (name: string option) =

  let mutable _name : string option = name
  let mutable _parent : TestMetadata option = None

  /// The test name. It doesn't contain the parameters.
  member __.Name = _name

  /// Parent metadata. Storing by TestCollector.
  member __.Parent = _parent

  /// Metadata unique name (if the test has parameters then the value contains them).
  abstract UniqueName : string
  default this.UniqueName = this.SymbolName

  /// Metadata symbol name.
  member this.SymbolName =
    match _parent with
    | Some parent -> parent.SymbolName + "." + TestMetadata.safeName(_name)
    | None -> TestMetadata.safeName(None) + "." + TestMetadata.safeName(_name)

  /// Metadata display name.
  abstract DisplayName : string
  default this.DisplayName = this.UniqueName

  /// Metadata string.
  override this.ToString() = this.UniqueName

  /// For internal use only.
  static member internal safeName(name: string option) = 
    match name with
    | Some name -> name
    | None -> "[Unknown]"

  /// For internal use only.
  member internal this.Fixup(name: string, parent: TestMetadata option) =
    match (_name, _parent) with
    | (Some _, Some _) -> ()
    | (Some _, None) ->
      _parent <- parent
    | (None, Some _) ->
      _name <- Some name
    | (None, None) ->
      _name <- Some name
      _parent <- parent

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

  /// The test parameters.
  /// If the test has no parameters then the value is empty list.
  member __.Parameters = parameters
    
  /// For internal use only.
  abstract member OnAsyncRun: unit -> Async<TestResult>

  /// Execute this test case.
  member this.AsyncRun() = this.OnAsyncRun()
  
  /// Execute this test case.
  /// TODO: Omit all synch caller.
  //[<Obsolete>]
  member this.Run() = this.OnAsyncRun() |> Async.RunSynchronously

  /// Metadata unique name (if the test has parameters then the value contains them).
  override this.UniqueName =
    match this.Parameters |> Seq.isEmpty with
    | true -> this.SymbolName
    | false -> sprintf "%s(%s)" this.SymbolName (this.Parameters |> PrettyPrinter.printAll)

  /// Metadata display name.
  override this.DisplayName =
    let name = TestMetadata.safeName this.Name
    match this.Parameters |> Seq.isEmpty with
    | true -> this.SymbolName
    | false -> sprintf "%s(%s)" name (this.Parameters |> PrettyPrinter.printAll)

//  interface ITestCase with
//    member this.Parameters = this.Parameters
//    member __.AsyncRun() = new InvalidOperationException() |> raise  // HACK

/// Non generic view for test result. (fake base type)
and TestResult =
  abstract TestCase: TestCase
  abstract Exceptions: exn[]
  abstract Duration: TimeSpan

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

  /// For internal use only.
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
and TestResult<'T> =
  /// This case represents the error.
  | Error of TestCase<'T> * exn list * NotPassedCause list * TimeSpan
  /// This case represents that all of the assertions is finished.
  | Done of TestCase<'T> * NonEmptyList<AssertionResult<'T>> * TimeSpan
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

    interface TestResult with
      member this.TestCase = this.TestCase :> TestCase
      member this.Exceptions = this.Exceptions
      member this.Duration = this.Duration

//    interface ITestResult with
//      member this.TestCase = this.TestCase :> TestCase
//      member this.Exceptions = this.Exceptions
//      member this.Duration = this.Duration

///////////////////////////////////////////////////////////////////////////

/// Test context class. (structuring nested test node)
[<Sealed>]
type Context (name: string option, children: TestMetadata seq) =
  inherit TestMetadata (name)

  /// Child tests.
  member __.Children = children

  override this.ToString() =
    sprintf "%s(%A)" this.SymbolName children
