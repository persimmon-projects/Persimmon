namespace Persimmon

module String =

  let bar width (barChar: char) (center: string) =
    let barLen = width - center.Length - 2
    let left = barLen / 2
    let res = (String.replicate left (string barChar)) + " " + center + " "
    let right = width - res.Length
    res + (String.replicate right (string barChar))

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ResultNode =

  open System
  open ActivePatterns

  let private indentStr indent =
    String.replicate indent " "

  let private causesToStrs indent (causes: (int option * NotPassedCause) seq) =
    causes
    |> Seq.mapi (fun i (l, (Skipped c | Violated c)) -> (i + 1, l,  c))
    |> Seq.collect (fun (i, l, c) ->
      seq {
        match c.Split([|"\r\n";"\r";"\n"|], StringSplitOptions.None) |> Array.toList with
        | [] -> yield ""
        | x::xs ->
          let no = (string i) + ". "
          let x, xs =
            match l with
            | Some l -> (sprintf "Line Number: %d" l, x::xs)
            | None -> (x, xs)
          yield indent + no + x
          yield! xs |> Seq.map (fun x -> indent + (String.replicate no.Length " ") + x)
      })

  let private exnsToStrs indent (exns: ExceptionWrapper seq) =
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
      })

  let rec toStrs indent = function
  | EndMarker -> Seq.empty
  | ContextResult contextResult ->
    let rs = contextResult.Results |> Seq.collect (toStrs (indent + 1))
    if Seq.isEmpty rs then Seq.empty
    else
      seq {
        yield (indentStr indent) + "begin " + contextResult.Context.DisplayName
        yield! rs
        yield (indentStr indent) + "end " + contextResult.Context.DisplayName
      }
  | TestResult testResult ->
    match testResult.Exceptions with
    | exns when exns.Length >= 1 ->
      seq {
        let indent = indentStr indent
        yield indent + "FATAL ERROR: " + testResult.TestCase.DisplayName
        if testResult.AssertionResults.Length >= 1 then
          yield (String.bar (70 - indent.Length) '-' "finished assertions")
          yield!
            testResult.AssertionResults
            |> Seq.choose (fun ar -> ar.Status |> Option.map (fun cause -> (ar.LineNumber, cause)))
            |> causesToStrs indent
        yield indent + (String.bar (70 - indent.Length) '-' "exceptions")
        yield! exns |> Seq.toList |> List.rev |> exnsToStrs indent
      }
    | _ ->
      seq {
        let indent = indentStr indent
        match (testResult.AssertionResults |> AssertionResult.Seq.typicalResult).Status with
        | None -> ()
        | Some (Skipped _) ->
          yield indent + "Assertion skipped: " + testResult.TestCase.DisplayName
        | Some (Violated _) ->
          yield indent + "Assertion Violated: " + testResult.TestCase.DisplayName
          yield!
            testResult.AssertionResults
            |> Seq.choose (fun ar -> ar.Status |> Option.map (fun cause -> (ar.LineNumber, cause)))
            |> causesToStrs indent
      }
