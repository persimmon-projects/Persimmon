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

    open System.Xml.Linq
    open SummaryFormatter

    let private xname name = XName.Get(name)

    let private timeSpanToStr (span: TimeSpan) = sprintf "%.3f" span.TotalSeconds

    let private toXElements result =
      let element () = XElement(xname "testsuite")
      let summary (children: ResizeArray<XElement * Summary>) (result: ResultNode) (suite: XElement) =
        let total = result |> Summary.collectSummary Summary.empty
        let summary =
          children
          |> Seq.fold (fun acc (_, x) ->
            {
              Run = acc.Run - x.Run
              Skipped = acc.Skipped - x.Skipped
              Violated = acc.Violated - x.Violated
              Error = acc.Error - x.Error
              Duration = acc.Duration
            }
          ) total
        suite.Add(
          XAttribute(xname "timestamp", DateTime.Now.ToString()),
          XAttribute(xname "tests", summary.Run),
          XAttribute(xname "failures", summary.Violated),
          XAttribute(xname "errors", summary.Error),
          XAttribute(xname "skipped", summary.Skipped)
        )
        (suite, summary)
      let rec inner (results: ResizeArray<XElement * Summary>) (acc: XElement) = function
      | EndMarker -> acc
      | ContextResult contextResult ->
        let name = xname "name"
        match acc.Attribute(name) with
        | null ->
          acc.Add(XAttribute(name, contextResult.Context.DisplayName))
          contextResult.Results |> Seq.fold (inner results) acc
        | x ->
          let suite = element ()
          suite.Add(XAttribute(name, (sprintf "%s.%s" x.Value contextResult.Context.DisplayName)))
          let children = ResizeArray<XElement * Summary>()
          contextResult.Results
          |> Seq.fold (inner children) suite
          |> summary children contextResult
          |> results.Add
          results.AddRange(children)
          acc
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
      let results = ResizeArray<XElement * Summary>()
      inner results (element ()) result
      |> summary results result
      |> results.Add
      results
      |> Seq.choose (fun (x, _) ->
        if Seq.isEmpty <| x.Elements() then None
        else Some x
      )

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
          let xdocument = XDocument(XElement(xname "testsuites", results |> Seq.collect toXElements) |> addSummary watch results)
          Writable.xdocument(xdocument)
      }
