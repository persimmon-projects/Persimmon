namespace Persimmon

open System
open System.Reflection

///////////////////////////////////////////////////////////////////////////
// Test interfaces.
// These interface types only use for non generic viewing.

/// Non generic view for metadata.
type ITestMetadata =
  abstract Name : string option
  abstract Parent : ITestMetadata option
  abstract UniqueName : string
  abstract SymbolName : string
  abstract DisplayName : string

/// Non generic view for test case.
and ITestCase =
  inherit ITestMetadata
  abstract Parameters : (Type * obj) seq

/// Non generic view for test result.
and ITestResult =
  abstract TestCase : ITestCase
  abstract Exceptions : exn []
  abstract Duration : TimeSpan

namespace Persimmon.Output

open Persimmon

/// This interface abstraction how output results running on tests.
type IReporter =
  abstract ReportProgress: ITestResult -> unit
  abstract ReportSummary: ITestResult seq -> unit
  abstract ReportError: string -> unit
