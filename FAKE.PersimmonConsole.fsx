module Fake.PersimmonConsole

#load ".fake/build.fsx/intellisense.fsx"

open System
open System.IO
open System.Text
open Fake.Core
open Fake.Testing.Common
open Fake.IO
open Fake.IO.FileSystemOperators

type OutputDestination =
  | Console
  | PlaneTextFile of path: string
  | XmlFile of path: string

type ErrorOutputDestination =
  | Console
  | File of path: string

type PersimmonParams = {
  ToolPath: string
  NoProgress: bool
  Parallel: bool
  Output: OutputDestination
  Error: ErrorOutputDestination
  TimeOut : TimeSpan
  ErrorLevel: TestRunnerErrorLevel
}

let PersimmonDefaults = {
  ToolPath = ProcessUtils.tryFindFile [ "." @@ "tools" @@ "Persimmon.Console" ] "Persimmon.Console.exe" |> Option.defaultValue "Persimmon.Console.exe"
  NoProgress = false
  Parallel = false
  Output = OutputDestination.Console
  Error = Console
  TimeOut = TimeSpan.FromMinutes 5.
  ErrorLevel = Error
}

let buildPersimmonArgs parameters assemblies =
  let output, outputText =
    match parameters.Output with
    | OutputDestination.Console -> false, []
    | PlaneTextFile path ->
      let outputPath = path.TrimEnd Path.DirectorySeparatorChar
      (true, [ sprintf "--output:%s" outputPath ])
    | XmlFile path ->
      let outputPath = path.TrimEnd Path.DirectorySeparatorChar
      (true, [ sprintf "--output:%s" outputPath; "--format:xml" ])
  let error, errorText =
    match parameters.Error with
    | Console -> false, ""
    | File path -> (true, sprintf "--error:%s" <| path.TrimEnd Path.DirectorySeparatorChar)
  seq {
    if output then
      yield! outputText
    if error then
      yield errorText
    if parameters.NoProgress then
      yield "--no-progress"
    if parameters.Parallel then
      yield "--parallel"

    yield! assemblies
  }

let Persimmon setParams assemblies =
  let details = String.separated ", " (assemblies |> Seq.map (fun p -> FileInfo.ofPath(p).Name))
  use traceTask = Trace.traceTask "Persimmon" details
  
  let parameters = setParams PersimmonDefaults
  let args = buildPersimmonArgs parameters assemblies
  let procResult =
    Command.RawCommand(parameters.ToolPath, Arguments.OfArgs args)
    |> CreateProcess.fromCommand
    |> CreateProcess.withTimeout parameters.TimeOut
    |> Proc.run
  if 0 <> procResult.ExitCode then 
    sprintf "Persimmon test failed on %s." details
    |> match parameters.ErrorLevel with
       | Error | FailOnFirstError -> failwith
       | DontFailBuild -> Trace.traceImportant
    traceTask.MarkFailed()
  else
    traceTask.MarkSuccess()