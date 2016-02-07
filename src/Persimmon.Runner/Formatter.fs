namespace Persimmon.Output

type IFormatter<'T> =
  abstract Format: 'T -> IWritable

module Formatter =
  open System
  open System.Diagnostics
  open Persimmon
  open Persimmon.ActivePatterns

  module ProgressFormatter =
    let dot =
      { new IFormatter<ITestResult> with
          member x.Format(test: ITestResult): IWritable = 
            match test with
            | ContextResult _ctx -> Writable.doNothing
            | EndMarker -> Writable.newline
            | TestResult tr ->
                match tr with
                | Error _ -> Writable.char 'E'
                | Done (_, res, _) ->
                    let typicalRes = res |> AssertionResult.NonEmptyList.typicalResult
                    match typicalRes with
                    | Passed _ -> Writable.char '.'
                    | NotPassed (Skipped _) -> Writable.char '_'
                    | NotPassed (Violated _) -> Writable.char 'x'
          }

  module SummaryFormatter =
    let private tryGetCause = function
    | Passed _ -> None
    | NotPassed cause -> Some cause

    let private indentStr indent =
      String.replicate indent " "

    let private bar width (barChar: char) (center: string) =
      let barLen = width - center.Length - 2
      let left = barLen / 2
      let res = (String.replicate left (string barChar)) + " " + center + " "
      let right = width - res.Length
      res + (String.replicate right (string barChar))

    let private causesToStrs indent (causes: NotPassedCause list) =
      causes
      |> Seq.mapi (fun i (Skipped c | Violated c) -> (i + 1, c))
      |> Seq.collect (fun (i, c) ->
          seq {
            match c.Split([|"\r\n";"\r";"\n"|], StringSplitOptions.None) |> Array.toList with
            | [] -> yield ""
            | x::xs ->
                let no = (string i) + ". "
                yield indent + no + x
                yield! xs |> Seq.map (fun x -> indent + (String.replicate no.Length " ") + x)
          }
         )

    let private exnsToStrs indent (exns: exn list) =
      exns
      |> Seq.mapi (fun i exn -> (i + 1, exn))
      |> Seq.collect (fun (i, exn) ->
          seq {
            match exn.ToString().Split([|"\r\n";"\r";"\n"|], StringSplitOptions.None) |> Array.toList with
            | [] -> yield ""
            | x::xs ->
                let no = (string i) + ". "
                yield indent + no + x
                yield! xs |> Seq.map (fun x -> indent + (String.replicate no.Length " ") + x)
          }
         )

    let rec private toStrs indent = function
    | EndMarker -> Seq.empty
    | ContextResult ctx ->
        let rs = ctx.Children |> Seq.collect (toStrs (indent + 1))
        if Seq.isEmpty rs then Seq.empty
        else
          seq {
            yield (indentStr indent) + "begin " + ctx.Name
            yield! rs
            yield (indentStr indent) + "end " + ctx.Name
          }
    | TestResult tr ->
        match tr with
        | Error (meta, es, res, _) ->
            seq {
              let indent = indentStr indent
              yield indent + "FATAL ERROR: " + meta.FullName
              if not (res.IsEmpty) then
                yield (bar (70 - indent.Length) '-' "finished assertions")
                yield! res |> causesToStrs indent
              yield indent + (bar (70 - indent.Length) '-' "exceptions")
              yield! es |> List.rev |> exnsToStrs indent
            }
        | Done (meta, res, _) ->
            seq {
              let indent = indentStr indent
              match res |> AssertionResult.NonEmptyList.typicalResult with
              | Passed _ -> ()
              | NotPassed (Skipped _) ->
                  yield indent + "Assertion skipped: " + meta.FullName
              | NotPassed (Violated _) ->
                  yield indent + "Assertion Violated: " + meta.FullName
              let causes = res |> NonEmptyList.toList |> List.choose tryGetCause
              yield! causes |> causesToStrs indent
            }

    type Summary = {
      Run: int
      Skipped: int
      Violated: int
      Error: int
      Duration: TimeSpan
    }
    with
      override this.ToString() =
        sprintf "run: %d, error: %d, violated: %d, skipped: %d, duration: %O" this.Run this.Error this.Violated this.Skipped this.Duration

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Summary =
      let empty = { Run = 0; Skipped = 0; Violated = 0; Error = 0; Duration = TimeSpan.Zero }

      let rec collectSummary summary = function
      | EndMarker -> summary
      | ContextResult ctx ->
          ctx.Children |> Seq.fold collectSummary summary
      | TestResult (Error (_, _, _, d)) ->
          { summary with Run = summary.Run + 1; Error = summary.Error + 1 }
      | TestResult (Done (_, res, d)) ->
          match res |> AssertionResult.NonEmptyList.typicalResult with
          | Passed _ -> { summary with Run = summary.Run + 1 }
          | NotPassed (Skipped _) -> { summary with Run = summary.Run + 1; Skipped = summary.Skipped + 1 }
          | NotPassed (Violated _) -> { summary with Run = summary.Run + 1; Violated = summary.Violated + 1 }

    let normal (watch: Stopwatch) =
      { new IFormatter<ITestResult seq> with
          member x.Format(results: ITestResult seq): IWritable = 
            Writable.stringSeq begin
              seq {
                yield! results |> Seq.collect (toStrs 0)
                yield bar 70 '=' "summary"
                let summary = results |> Seq.fold Summary.collectSummary { Summary.empty with Duration = watch.Elapsed }
                yield string summary
              }
            end
          }

  module ErrorFormatter =
    let normal =
      { new IFormatter<string> with
          member __.Format(message: string) = 
            Writable.stringSeq (Seq.singleton message)
          }

  module XmlFormatter =

    open System.Xml
    open System.Xml.Linq
    open SummaryFormatter

    let private xname name = XName.Get(name)

    let private toXDocument result =
      let rec inner (acc: XElement) = function
      | EndMarker -> acc
      | ContextResult ctx ->
        let name =
          match acc.Attribute(xname "name") with
          | null -> ctx.Name
          | x ->
            x.Remove()
            sprintf "%s.%s" x.Value ctx.Name
        let nameAttr = XAttribute(xname "name", name)
        acc.Add(nameAttr)
        ctx.Children |> Seq.fold inner acc
      | TestResult tr ->
        let testCase = XElement(xname "testcase", XAttribute(xname "name", tr.FullName))
        match tr with
        | Error (meta, es, res, duration) as e ->
          testCase.Add(
            XElement(xname "error",
              XAttribute(xname "type", e.GetType().FullName),
              // TODO: fix message
              XAttribute(xname "message", res.ToString() + es.ToString())),
            XAttribute(xname "time", duration.ToString()))
        | Done (meta, res, duration) ->
          match res |> AssertionResult.NonEmptyList.typicalResult with
          | Passed _ ->
            testCase.Add(
              XAttribute(xname "time", duration.ToString()))
          | NotPassed (Skipped message) ->
            testCase.Add(
              XElement(xname "skipped", message),
              XAttribute(xname "time", duration.ToString()))
          | NotPassed (Violated message as v) ->
            testCase.Add(
              XElement(xname "failure",
                XAttribute(xname "type", v.GetType().FullName),
                // TODO: fix message
                XAttribute(xname "message", message)),
              XAttribute(xname "time", duration.ToString()))
        acc.Add(testCase)
        acc
      let suite = inner (XElement(xname "testsuite")) result
      let summary = result |> Summary.collectSummary Summary.empty
      suite.Add(
        XAttribute(xname "tests", summary.Run),
        XAttribute(xname "failures", summary.Violated),
        XAttribute(xname "errors", summary.Error),
        XAttribute(xname "skipped", summary.Skipped))
      suite

    let addSummary (watch: Stopwatch) results (suites: XElement) =
      let summary = results |> Seq.fold Summary.collectSummary { Summary.empty with Duration = watch.Elapsed }
      suites.Add(
        XAttribute(xname "tests", summary.Run),
        XAttribute(xname "failures", summary.Violated),
        XAttribute(xname "errors", summary.Error),
        XAttribute(xname "time", summary.Duration.ToString()))
      suites

    let junitStyle watch =
      { new IFormatter<ITestResult seq> with
        member __.Format(results: ITestResult seq) =
          let xdocument = XDocument(XElement(xname "testsuites", results |> Seq.map toXDocument) |> addSummary watch results)
          Writable.xdocument(xdocument)
      }
