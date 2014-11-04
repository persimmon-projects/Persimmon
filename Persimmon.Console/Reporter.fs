namespace global

open System
open Persimmon

type Reporter (progressPrinter: Printer<ITest>, summaryPrinter: Printer<ITest seq>, errorPrinter: Printer<string>) =
  let results = ResizeArray<ITest>()

  member __.ReportProgress(test: ITest) =
    results.Add(test)
    progressPrinter.Print(test)

  member __.ReportSummary() =
    summaryPrinter.PrintLine(results)

  member __.ReportError(str: string) =
    errorPrinter.PrintLine(str)

  interface IDisposable with
    member __.Dispose() = 
      progressPrinter.Dispose()
      summaryPrinter.Dispose()
      errorPrinter.Dispose()
    