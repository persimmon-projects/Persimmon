namespace Persimmon.Output

open System
open Persimmon

type Reporter
  (
    progressPrinter: Printer<ITestResult>,
    summaryPrinter: Printer<ITestResult seq>,
    errorPrinter: Printer<string>
  ) =

  member __.ReportProgress(test: ITestResult) =
    progressPrinter.Print(test)

  member __.ReportSummary(rootTests: ITestResult seq) =
    summaryPrinter.Print(rootTests)

  member __.ReportError(message: string) =
    errorPrinter.Print(message)

  interface IDisposable with
    member __.Dispose() = 
      [ progressPrinter.Dispose; summaryPrinter.Dispose; errorPrinter.Dispose ]
      |> List.iter (fun d -> try d () with _ -> ())
    