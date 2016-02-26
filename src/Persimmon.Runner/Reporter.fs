namespace Persimmon.Output

open System
open Persimmon

type Reporter
  (
    progressPrinter: Printer<ITestResultNode>,
    summaryPrinter: Printer<ITestResultNode seq>,
    errorPrinter: Printer<string>
  ) =

  member __.ReportProgress(test: ITestResultNode) =
    progressPrinter.Print(test)

  member __.ReportSummary(rootTests: ITestResultNode seq) =
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
