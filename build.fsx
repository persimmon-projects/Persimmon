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
open Fake.Tools.Git

Target.initEnvironment ()

let outDir = "bin"

let configuration = Environment.environVarOrDefault "configuration" "Release"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "persimmon-projects"
let gitHome = "git@github.com:" + gitOwner

// The name of the project on GitHub
let gitName = "Persimmon"

// The url for the raw files hosted
let gitRaw = Environment.environVarOrDefault "gitRaw" "https://raw.github.com/persimmon-projects"

let release = ReleaseNotes.load "RELEASE_NOTES.md"

Target.create "CopyBinaries" (fun _ ->
  !! "src/**/*.??proj"
  |>  Seq.map (fun f -> ((System.IO.Path.GetDirectoryName f) @@ "bin" @@ configuration, outDir @@ (System.IO.Path.GetFileNameWithoutExtension f)))
  |>  Seq.iter (fun (fromDir, toDir) -> Shell.copyDir toDir fromDir (fun _ -> true))
)

// --------------------------------------------------------------------------------------
// Clean build results

Target.create "Clean" (fun _ ->
  !! "**/bin"
  ++ "**/obj"
  |> Shell.cleanDirs 
)

Target.create "CleanDocs" (fun _ ->
  !! "docs/output"
  |> Shell.cleanDirs
)

// --------------------------------------------------------------------------------------
// Build library & test project

Target.create "Build" (fun _ ->
  !! "*.sln"
  |> Seq.iter (DotNet.build (fun args ->
    { args with
        Configuration = DotNet.BuildConfiguration.fromString configuration
    }))
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

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target.create "NuGet" (fun _ ->
  use trace = Trace.traceTask "PaketPack" "."
  let args =
    Arguments.OfArgs([ "pack" ])
    |> Arguments.append [ "--version"; release.NugetVersion ]
    |> Arguments.append [ "--release-notes"; System.Net.WebUtility.HtmlEncode(release.Notes |> String.concat System.Environment.NewLine) ]
    |> Arguments.append [ outDir ]
  let result = DotNet.exec id "paket" (args.ToWindowsCommandLine)
  if not result.OK then failwith "Error during packing."
  trace.MarkSuccess()
)

Target.create "PublishNuget" (fun _ ->
  let apiKey = Environment.environVar "api-key"
  if (String.isNullOrEmpty apiKey = false) then TraceSecrets.register "<api-key>" apiKey

  !! (outDir @@ "/**/*.nupkg")
  |> Seq.iter (fun package ->
    use trace = Trace.traceTask "PaketPublish" package
    let args =
      Arguments.OfArgs([ "push" ])
      |> Arguments.appendNotEmpty "--api-key" apiKey
      |> Arguments.append([ package ])
    let result = DotNet.exec id "paket" (args.ToWindowsCommandLine)
    if not result.OK then failwithf "Error during pushing %s" package
    trace.MarkSuccess()
  )
)

// --------------------------------------------------------------------------------------
// Generate the documentation

Target.create "CopyCommonDocFiles" (fun _ ->
  Docs.copyCommonFiles()
)

Target.create "GenerateHelp" (fun _ ->
  Docs.generateHelp()
)

Target.create "GenerateReferenceDocs" (fun _ ->
  Docs.generateReference()
)

Target.create "GenerateDocs" ignore

Target.create "ReleaseDocs" (fun _ ->
  let tempDocsDir = "temp/gh-pages"
  Shell.cleanDir tempDocsDir
  Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") "gh-pages" tempDocsDir

  Shell.copyRecursive Docs.output tempDocsDir true |> Trace.tracefn "%A"
  Staging.stageAll tempDocsDir
  Commit.exec tempDocsDir (sprintf "Update generated documentation for version %s" release.NugetVersion)
  Branches.push tempDocsDir
)

Target.create "All" ignore

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
  ==> "ReleaseDocs"

Target.runOrDefault "All"
