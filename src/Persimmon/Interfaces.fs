namespace Persimmon

open System
open System.Reflection

///////////////////////////////////////////////////////////////////////////
// Test interfaces

/// Non generic view for metadata.
type ITestMetadata =
  abstract Name : string option
  abstract Parent : ITestMetadata option
  abstract SymbolName : string
  abstract UniqueName : string

/// Non generic view for test case.
and ITestCase =
  inherit ITestMetadata
  abstract Parameters : (Type * obj) seq
  abstract Run : unit -> ITestResult

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
