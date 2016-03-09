namespace Persimmon

open System

/// This interface total abstraction represents a test metadata. (non-generic view)
type ITestNode =
  /// The test name. It doesn't contain the parameters.
  abstract Name: string option
  /// The test defined type. Storing by TestCollector.
  abstract DeclaredType: Type option

/// This interface represents a test case metadata. (non-generic view)
type ITestMetadata =
  inherit ITestNode
  /// The test name(if the test has parameters then the value contains them).
  abstract FullName : string
  /// The test parameters.
  /// If the test has no parameters then the value is empty list.
  abstract Parameters: (Type * obj) list

/// The interface that is treated as tests by Persimmon. (non-generic view)
type ITestObject =
  inherit ITestNode

/// This interface represents a test result. (non-generic view)
/// You should use the ActivePatterns
/// if you want to process derived objects through this interface.
type ITestResultNode =
  /// The test name. It doesn't contain the parameters.
  abstract Name: string
  /// The test defined type. Storing by TestCollector.
  abstract DeclaredType: Type

type ITestResult =
  inherit ITestResultNode
  /// The test name(if the test has parameters then the value contains them).
  abstract FullName : string
  /// The test parameters.
  /// If the test has no parameters then the value is empty list.
  abstract Parameters: (Type * obj) list
  /// The test result
  abstract Outcome : string // TODO: lack of informations (ex: exn in failed)

/// This interface represents a test case. (non-generic view)
/// In order to run the test represented this class, use the "Run" method.
type ITestCase =
  inherit ITestObject
  inherit ITestMetadata
  /// (For internal use only)
  abstract CreateAdditionalMetadataIfNeed: string * Type -> ITestCase
  abstract Run: unit -> ITestResult

namespace Persimmon.Output

open Persimmon

/// This interface abstraction how output results running on tests.
type IReporter =
  abstract ReportProgress: ITestResultNode -> unit
  abstract ReportSummary: ITestResultNode seq -> unit
  abstract ReportError: string -> unit
