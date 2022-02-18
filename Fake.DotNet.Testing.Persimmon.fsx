module Fake.DotNet.Testing.Persimmon

#load ".fake/build.fsx/intellisense.fsx"

open System
open System.IO
open Fake.Core
open Fake.IO
open Fake.Testing.Common

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
  ToolPath = ""
  NoProgress = false
  Parallel = false
  Output = OutputDestination.Console
  Error = Console
  TimeOut = TimeSpan.FromMinutes 5.
  ErrorLevel = Error
}

let internal buildPersimmonArgs parameters =
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
  }

let Persimmon setParams =
  let parameters = setParams PersimmonDefaults
  let details = FileInfo.ofPath(parameters.ToolPath).Name
  use traceTask = Trace.traceTask "Persimmon" details
  let args = buildPersimmonArgs parameters
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