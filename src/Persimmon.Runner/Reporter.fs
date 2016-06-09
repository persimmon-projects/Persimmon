namespace Persimmon.Output

open System
open Persimmon

open Persimmon

/// This interface abstraction how output results running on tests.
type IReporter =
  abstract ReportProgress: ResultNode -> unit
  abstract ReportSummary: ResultNode seq -> unit
  abstract ReportError: string -> unit

type Reporter
  (
    progressPrinter: Printer<ResultNode>,
    summaryPrinter: Printer<ResultNode seq>,
    errorPrinter: Printer<string>
  ) =

  member __.ReportProgress(test: ResultNode) =
    progressPrinter.Print(test)

  member __.ReportSummary(rootTests: ResultNode seq) =
    summaryPrinter.Print(rootTests)

  member __.ReportError(message: string) =
    errorPrinter.Print(message)

  interface IDisposable with
    member __.Dispose() = 
      [ progressPrinter.Dispose; summaryPrinter.Dispose; errorPrinter.Dispose ]
      |> List.iter (fun d -> try d () with _ -> ())
    
  interface IReporter with
    member this.ReportProgress test = this.ReportProgress test
    member this.ReportSummary tests = this.ReportSummary tests
    member this.ReportError message = this.ReportError message
