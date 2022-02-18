#r "paket: groupref Build //"
#load ".fake/build.fsx/intellisense.fsx"

#load "FAKE.PersimmonConsole.fsx"
#load "Fake.DotNet.Testing.Persimmon.fsx"
#load "generate.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.DotNet.Testing.Persimmon

Target.initEnvironment ()

let outDir = "bin"

let configuration = Environment.environVarOrDefault "configuration" "Release"

let release = ReleaseNotes.load "RELEASE_NOTES.md"

Target.create "Clean" (fun _ ->
  !! "**/bin"
  ++ "**/obj"
  |> Shell.cleanDirs 
)

Target.create "Build" (fun _ ->
  !! "*.sln"
  |> Seq.iter (DotNet.build (fun args ->
    { args with
        Configuration = DotNet.BuildConfiguration.fromString configuration
    }))
)

Target.create "CopyBinaries" (fun _ ->
  !! "src/**/*.??proj"
  |>  Seq.map (fun f -> ((System.IO.Path.GetDirectoryName f) @@ "bin" @@ configuration, outDir @@ (System.IO.Path.GetFileNameWithoutExtension f)))
  |>  Seq.iter (fun (fromDir, toDir) -> Shell.copyDir toDir fromDir (fun _ -> true))
)

let consoleRunnerTestAssemblies = !! ("tests/**/bin/" @@ configuration @@ "/net462/*Tests.dll")
let exeTestAssemblies = !! ("tests/**/bin/" @@ configuration @@ "/*/*Tests.exe")

Target.create "RunTests" (fun _ ->
  consoleRunnerTestAssemblies
  |> Fake.PersimmonConsole.Persimmon (fun p ->
  { p with
      ToolPath = ProcessUtils.findFile [ "./src/Persimmon.Console/bin/" @@ configuration @@ "/net462" ] "Persimmon.Console.exe"
      Output = Fake.PersimmonConsole.OutputDestination.XmlFile "TestResult.Console.xml"
  })

  exeTestAssemblies
  |> Seq.iter (fun exe ->
    let fileName = FileInfo.ofPath(exe).Name
    Persimmon (fun p ->
      { p with
          ToolPath = exe
          Output = OutputDestination.XmlFile ($"TestResult.{fileName}.xml")
      })
  )
)

Target.create "CleanDocs" (fun _ ->
  !! "docs/output"
  |> Shell.cleanDirs
)

Target.create "CopyCommonDocFiles" (fun _ ->
  Docs.copyCommonFiles()
)

Target.create "GenerateHelp" (fun _ ->
  Docs.generateHelp()
)

Target.create "GenerateReferenceDocs" (fun _ ->
  Docs.generateReference()
)

Target.create "All" ignore
Target.create "GenerateDocs" ignore

"Clean"
  ==> "Build"
  ==> "CopyBinaries"
  ==> "RunTests"
  ==> "All"

"CleanDocs"
  ==> "CopyCommonDocFiles"
  ==> "GenerateHelp"
  ==> "GenerateReferenceDocs"
  ==> "GenerateDocs"

Target.runOrDefault "All"
