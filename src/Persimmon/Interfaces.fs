namespace Persimmon

open System

/// This interface total abstraction represents a test metadata. (non-generic view)
type ITestNode =
  /// The test name. It doesn't contain the parameters.
  abstract Name: string option

/// This interface represents a test case metadata. (non-generic view)
type ITestCaseNode =
  inherit ITestNode
  /// The test name(if the test has parameters then the value contains them).
  abstract FullName : string
  /// The test parameters.
  /// If the test has no parameters then the value is empty list.
  abstract Parameters: (Type * obj) list

/// The interface that is treated as tests by Persimmon. (non-generic view)
type ITestObject =
  inherit ITestNode
  abstract SetNameIfNeed: string -> ITestObject

/// This interface represents a test result. (non-generic view)
/// You should use the ActivePatterns
/// if you want to process derived objects through this interface.
type ITestResult =
  inherit ITestNode

/// This interface represents a test case. (non-generic view)
/// In order to run the test represented this class, use the "Run" method.
type ITestCase =
  inherit ITestObject
  inherit ITestCaseNode
  abstract Run: unit -> ITestResult
