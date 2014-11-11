namespace Persimmon.Output

type IFormatter<'T> =
  abstract Format: 'T -> IWritable

module Formatter =
  open System
  open Persimmon
  open Persimmon.ActivePatterns

  module ProgressFormatter =
    let dot =
      { new IFormatter<ITestResult> with
          member x.Format(test: ITestResult): IWritable = 
            match test with
            | ContextResult ctx ->
                let writables = ctx.Children |> Seq.map (x.Format)
                Writable.group writables
            | TestResult tr ->
                match tr with
                | Break _ -> Writable.char 'E'
                | Done (_, res) ->
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

    let rec private toStrs indent = function
    | ContextResult ctx ->
        seq {
          yield (indentStr indent) + "begin " + ctx.Name
          yield! ctx.Children |> Seq.collect (toStrs (indent + 1))
          yield (indentStr indent) + "end " + ctx.Name
        }
    | TestResult tr ->
        match tr with
        | Break (meta, e, res) ->
            seq {
              let indent = indentStr indent
              yield indent + "FATAL ERROR: " + meta.FullName
              if not (res.IsEmpty) then
                yield (bar (70 - indent.Length) '-' "finished assertions")
                yield! res |> causesToStrs indent
              yield indent + (bar (70 - indent.Length) '-' "exception")
              yield!
                e.ToString().Split([|"\r\n";"\r";"\n"|], StringSplitOptions.None)
                |> Seq.map (fun line -> indent + line)
            }
        | Done (meta, res) ->
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
    }
    with
      override this.ToString() =
        sprintf "run: %d, error: %d, violated: %d, skipped: %d" this.Run this.Error this.Violated this.Skipped

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Summary =
      let empty = { Run = 0; Skipped = 0; Violated = 0; Error = 0 }

    let rec private collectSummary summary = function
    | ContextResult ctx ->
        ctx.Children |> Seq.fold collectSummary summary
    | TestResult (Break _) ->
        { summary with Run = summary.Run + 1; Error = summary.Error + 1 }
    | TestResult (Done (_, res)) ->
        match res |> AssertionResult.NonEmptyList.typicalResult with
        | Passed _ -> { summary with Run = summary.Run + 1 }
        | NotPassed (Skipped _) -> { summary with Run = summary.Run + 1; Skipped = summary.Skipped + 1 }
        | NotPassed (Violated _) -> { summary with Run = summary.Run + 1; Violated = summary.Violated + 1 }

    let normal =
      { new IFormatter<ITestResult seq> with
          member x.Format(results: ITestResult seq): IWritable = 
            Writable.stringSeq begin
              seq {
                yield! results |> Seq.collect (toStrs 0)
                yield bar 70 '=' "summary"
                let summary = results |> Seq.fold collectSummary Summary.empty
                yield string summary
              }
            end
          }