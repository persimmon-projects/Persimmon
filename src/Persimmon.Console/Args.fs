namespace global

open System
open System.IO
open Persimmon.Runner
open Persimmon.Output

type FormatType =
  | Normal
  | JUnitStyleXml

type Args = {
  Inputs: FileInfo list
  Output: FileInfo option
  Error: FileInfo option

  Format: FormatType

  NoProgress: bool
  Parallel: bool

  Help: bool
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Args =
  open System.Text
  open System.Diagnostics

  let empty = { Inputs = []; Output = None; Error = None; Format = Normal; NoProgress = false; Parallel = false; Help = false }

  let private (|StartsWith|_|) (prefix: string) (target: string) =
    if target.StartsWith(prefix) then
      Some (target.Substring(prefix.Length))
    else
      None

  let private (|Split2By|_|) (separator: string) (target: string) =
    match target.Split([| separator |], 2, StringSplitOptions.None) with
    | [| s1; s2 |] -> Some (s1, s2)
    | _ -> None

  let private toFileInfoList (str: string) =
    str.Split(',') |> Array.map (fun path -> FileInfo(path)) |> Array.toList

  let rec parse acc = function
  | [] -> acc
  | "--no-progress"::rest -> parse { acc with NoProgress = true } rest
  | "--parallel"::rest -> parse { acc with Parallel = true } rest
  | "--help"::rest -> parse { acc with Help = true } rest
  | (StartsWith "--" (Split2By ":" (key, value)))::rest ->
      match key with
      | "output" -> parse { acc with Output = Some (FileInfo(value)) } rest
      | "error" -> parse { acc with Error = Some (FileInfo(value)) } rest
      | "inputs" -> parse { acc with Inputs = acc.Inputs @ toFileInfoList value } rest
      | "format" ->
        let format =
          match value with
          | "xml" -> JUnitStyleXml
          | _ -> Normal
        parse { acc with Format = format } rest
      | other -> failwithf "unknown option: %s" other
  | other::rest -> parse { acc with Inputs = (FileInfo(other))::acc.Inputs } rest

  let progressPrinter (args: Args) =
    let progress = if args.NoProgress then IO.TextWriter.Null else Console.Out
    new Printer<_>(progress, Formatter.ProgressFormatter.dot)

  let reporter (watch: Stopwatch) (args: Args) =
    let progressPrinter = progressPrinter args
    let summary =
      let console = {
        Writer = Console.Out
        Formatter = Formatter.SummaryFormatter.normal watch
      }
      match args.Output, args.Format with
      | (Some file, JUnitStyleXml) ->
        let xml = {
          Writer = new StreamWriter(file.FullName, false, Encoding.UTF8) :> TextWriter
          Formatter = Formatter.XmlFormatter.junitStyle watch
        }
        [console; xml]
      | (Some file, Normal) ->
        let file = {
          Writer = new StreamWriter(file.FullName, false, Encoding.UTF8) :> TextWriter
          Formatter = console.Formatter
        }
        [console; file]
      | (None, Normal) -> [console]
      | (None, JUnitStyleXml) -> []
    let error =
      match args.Error with
      | Some file -> new StreamWriter(file.FullName, false, Encoding.UTF8) :> TextWriter
      | None -> Console.Error

    new Reporter(
      progressPrinter,
      new Printer<_>(summary),
      new Printer<_>(error, Formatter.ErrorFormatter.normal))

  let requireFileName (args: Args) =
    match args.Format with
    | JUnitStyleXml -> true
    | Normal -> false

  let help =
    """usage: Persimmon.Console.exe <options> <input>...

==== option ====
--output:<file>
    config the output file to print the result.
    print to standard output without this option.
--error:<file>
    config the output file to print the error.
    print to standard error without this option.
--inputs:<files>
    comma separated input files.
--format:[normal|xml]
    config the output format.
    'xml' format require '--output' option.
--no-progress
    disabled the report of progress.
--parallel
    run the tests asynchronous.
--help
    print this help message.
================
"""
