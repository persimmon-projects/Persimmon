namespace global

open System
open System.IO

type Args = {
  Inputs: FileInfo list
  Output: FileInfo option
  Error: FileInfo option

  NoProgress: bool

  Help: bool
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Args =
  let empty = { Inputs = []; Output = None; Error = None; NoProgress = false; Help = false }

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
  | "--help"::rest -> parse { acc with Help = true } rest
  | (StartsWith "--" (Split2By ":" (key, value)))::rest ->
      match key with
      | "output" -> parse { acc with Output = Some (FileInfo(value)) } rest
      | "error" -> parse { acc with Error = Some (FileInfo(value)) } rest
      | "inputs" -> parse { acc with Inputs = acc.Inputs @ toFileInfoList value } rest
      | other -> failwithf "unknown option: %s" other
  | other::rest -> parse { acc with Inputs = (FileInfo(other))::acc.Inputs } rest

  let help =
    """usage: Persimmon.Console.exe <options> <input>...

==== option ====
--output:<file>
    config the output file to print the result.
    print to standard output without this option.
--error:<file>
    config the output file to print the error.
    print to standard error without this option.
--input:<files>
    comma separated input files.
--no-progress
    disabled the report of progress.
--help
    print this help message.
================
"""