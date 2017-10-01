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
      { new IFormatter<ResultNode> with
          member x.Format(test: ResultNode): IWritable =
            match test with
            | ContextResult _ -> Writable.doNothing
            | EndMarker -> Writable.newline
            | TestResult testResult ->
                match Seq.isEmpty testResult.Exceptions with
                | false -> Writable.char 'E'
                | true ->
                  let ar = testResult.AssertionResults |> AssertionResult.Seq.typicalResult
                  match ar.Status with
                  | None -> Writable.char '.'
                  | Some (Skipped _) -> Writable.char '_'
                  | Some (Violated _) -> Writable.char 'x'
          }

  module SummaryFormatter =

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
      | ContextResult contextResult ->
          contextResult.Results |> Seq.fold collectSummary summary
      | TestResult (Error (_, _, _, _)) ->
          { summary with Run = summary.Run + 1; Error = summary.Error + 1 }
      | TestResult (Done (_, res, _)) ->
          match res |> AssertionResult.Seq.typicalResult with
          | Passed _ -> { summary with Run = summary.Run + 1 }
          | NotPassed (Skipped _) -> { summary with Run = summary.Run + 1; Skipped = summary.Skipped + 1 }
          | NotPassed (Violated _) -> { summary with Run = summary.Run + 1; Violated = summary.Violated + 1 }

    let normal (watch: Stopwatch) =
      { new IFormatter<ResultNode seq> with
          member x.Format(results: ResultNode seq): IWritable =
            Writable.stringSeq begin
              seq {
                yield! results |> Seq.collect (ResultNode.toStrs 0)
                yield String.bar 70 '=' "summary"
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

    let timeSpanToStr (span: TimeSpan) = sprintf "%.3f" span.TotalSeconds

    let private toXDocument result =
      let rec inner (acc: XElement) = function
      | EndMarker -> acc
      | ContextResult contextResult ->
        let name =
          match acc.Attribute(xname "name") with
          | null -> contextResult.Context.DisplayName
          | x ->
            x.Remove()
            sprintf "%s.%s" x.Value contextResult.Context.DisplayName
        let nameAttr = XAttribute(xname "name", name)
        acc.Add(nameAttr)
        contextResult.Results |> Seq.fold inner acc
      | TestResult tr ->
        let testCase =
          XElement(
            xname "testcase",
            XAttribute(xname "name", tr.TestCase.DisplayName)
          )
        match tr with
        | Error (_, es, res, duration) as e ->
          testCase.Add(
            XElement(xname "error",
              XAttribute(xname "type", e.GetType().FullName),
              // TODO: fix message
              XAttribute(xname "message", res.ToString() + es.ToString())),
            XAttribute(xname "time", timeSpanToStr duration))
        | Done (_, res, duration) ->
          match res |> AssertionResult.Seq.typicalResult with
          | Passed _ ->
            testCase.Add(
              XAttribute(xname "time", timeSpanToStr duration))
          | NotPassed (Skipped message) ->
            testCase.Add(
              XElement(xname "skipped", message),
              XAttribute(xname "time", timeSpanToStr duration))
          | NotPassed (Violated message as v) ->
            testCase.Add(
              XElement(xname "failure",
                XAttribute(xname "type", v.GetType().FullName),
                // TODO: fix message
                XAttribute(xname "message", message)),
              XAttribute(xname "time", timeSpanToStr duration))
        acc.Add(testCase)
        acc
      let suite = inner (XElement(xname "testsuite")) result
      let summary = result |> Summary.collectSummary Summary.empty
      suite.Add(
        XAttribute(xname "timestamp", DateTime.Now.ToString()),
        XAttribute(xname "tests", summary.Run),
        XAttribute(xname "failures", summary.Violated),
        XAttribute(xname "errors", summary.Error),
        XAttribute(xname "skipped", summary.Skipped)
      )
      suite

    let addSummary (watch: Stopwatch) results (suites: XElement) =
      let summary = results |> Seq.fold Summary.collectSummary { Summary.empty with Duration = watch.Elapsed }
      suites.Add(
        XAttribute(xname "tests", summary.Run),
        XAttribute(xname "failures", summary.Violated),
        XAttribute(xname "errors", summary.Error),
        XAttribute(xname "time", timeSpanToStr summary.Duration))
      suites

    let junitStyle watch =
      { new IFormatter<ResultNode seq> with
        member __.Format(results: ResultNode seq) =
          let xdocument = XDocument(XElement(xname "testsuites", results |> Seq.map toXDocument) |> addSummary watch results)
          Writable.xdocument(xdocument)
      }
