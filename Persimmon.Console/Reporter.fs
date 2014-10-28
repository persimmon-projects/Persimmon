namespace global

open System
open Persimmon

type Reporter (progressPrinter: Printer<TestResult<unit>>, summaryPrinter: Printer<TestResult<unit> seq>, errorPrinter: Printer<string>) =
  let results = ResizeArray<TestResult<unit>>()

  member __.ReportProgress(result: TestResult<_>) =
    results.Add(result)
    progressPrinter.Print(result)

  member __.ReportSummary() =
    summaryPrinter.PrintLine(results)

  member __.ReportError(str: string) =
    errorPrinter.PrintLine(str)

  interface IDisposable with
    member __.Dispose() = 
      progressPrinter.Dispose()
      summaryPrinter.Dispose()
      errorPrinter.Dispose()
    