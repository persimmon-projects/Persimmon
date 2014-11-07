namespace Persimmon

/// The result of each assertion.
type AssertionResult<'T> =
  | Passed of 'T
  | Skipped of string
  | Vaiolated of string

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

/// This class represents a nested test.
/// We can use this class for grouping of the tests.
type Context(name: string, children: TestObject list) =
  inherit TestObject ()

  /// The context name.
  member __.Name = name
  /// This is a list that has the elements represented the subcontext or the test case.
  member __.Children = children

  override this.ToString() =
    sprintf "Context(%A, %A)" name children

/// This class represents a test that has not been run yet.
/// In order to run the test represented this class, use the "Run" method.
type TestCase<'T>(metadata: TestMetadata, body: unit -> TestResult<'T>) =
  inherit TestObject ()

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
and TestResult<'T> =
    /// This case represents the break by exception.
    /// If exist some assertion results before thrown exception, this case holds them.
  | Break of TestMetadata * exn * AssertionResult<'T> list
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
