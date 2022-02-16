#r "paket:
nuget Fake.DotNet.Cli
nuget Fake.IO.FileSystem
nuget Fake.Core.Target
nuget Fake.Core.String
nuget Fake.Core.Process
nuget Fake.Core.Trace
nuget Fake.Testing.Common //"
#load ".fake/build.fsx/intellisense.fsx"

#load "FAKE.Persimmon.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

Target.initEnvironment ()

Target.create "Clean" (fun _ ->
  !! "**/bin"
  ++ "**/obj"
  |> Shell.cleanDirs 
)

Target.create "Build" (fun _ ->
  !! "src/**/*.*proj"
  ++ "tests/**/*.*proj"
  ++ "examples/**/*.*proj"
  |> Seq.iter (DotNet.build id)
)

let consoleRunnerTestAssemblies = !! "tests/**/bin/Release/net462/*Tests.dll"
let exeTestAssemblies = !! "tests/**/bin/Release/*/*Tests.exe"

Target.create "Test" (fun _ ->
  consoleRunnerTestAssemblies
  |> Fake.PersimmonConsole.Persimmon (fun p ->
  { p with
      ToolPath = ProcessUtils.findFile [ "./src/Persimmon.Console/bin/Release/net462" ] "Persimmon.Console.exe"
      Output = Fake.PersimmonConsole.OutputDestination.XmlFile "TestResult.xml"
  })
)

Target.create "All" ignore

"Clean"
  ==> "Build"
  ==> "All"

Target.runOrDefault "All"
