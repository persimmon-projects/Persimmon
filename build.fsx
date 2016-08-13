#r @"packages/build/FAKE/tools/FakeLib.dll"
#r @"packages/build/FAKE.Persimmon/lib/net451/FAKE.Persimmon.dll"
open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open System
open System.IO
#if MONO
#else
#load "packages/build/SourceLink.Fake/tools/Fake.fsx"
open SourceLink
#endif

let isDotnetInstalled = DotNetCli.isInstalled()

let outDir = "bin"

let project = "Persimmon"

// File system information
let solutionFile  = "Persimmon.sln"

// Pattern specifying assemblies to be tested using NUnit
let testAssemblies = "tests/**/bin/Release/*Tests*.dll"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "persimmon-projects"
let gitHome = "https://github.com/" + gitOwner

// The name of the project on GitHub
let gitName = "Persimmon"

// The url for the raw files hosted
let gitRaw = environVarOrDefault "gitRaw" "https://raw.github.com/persimmon-projects"

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps
// --------------------------------------------------------------------------------------

// Read additional information from the release notes document
let release = LoadReleaseNotes "RELEASE_NOTES.md"

// Helper active pattern for project types
let (|Fsproj|Csproj|Vbproj|) (projFileName:string) =
    match projFileName with
    | f when f.EndsWith("fsproj") -> Fsproj
    | f when f.EndsWith("csproj") -> Csproj
    | f when f.EndsWith("vbproj") -> Vbproj
    | _                           -> failwith (sprintf "Project file %s not supported. Unknown project type." projFileName)

// Generate assembly info files with the right version & up-to-date information
Target "AssemblyInfo" (fun _ ->
    let common = [
        Attribute.Product project
        Attribute.Version release.AssemblyVersion
        Attribute.FileVersion release.AssemblyVersion
        Attribute.InformationalVersion release.NugetVersion
    ]

    for suffix in [""; ".NET40"; ".Portable259"] do
      [
        Attribute.Title "Persimmon"
        Attribute.Description ""
      ] @ common
      |> CreateFSharpAssemblyInfo (sprintf "./src/Persimmon%s/AssemblyInfo.fs" suffix)

    for suffix in [""; ".NET40"] do
      [
          Attribute.Title "Persimmon.Runner"
          Attribute.Description ""
      ] @ common
      |> CreateFSharpAssemblyInfo (sprintf "./src/Persimmon.Runner%s/AssemblyInfo.fs" suffix)

    [
        Attribute.Title "Persimmon.Script"
        Attribute.Description ""
        Attribute.Guid "8B733755-9708-4F9C-A356-AE0C2EF1680D"
    ] @ common
    |> CreateFSharpAssemblyInfo "./src/Persimmon.Script/AssemblyInfo.fs"
)

Target "SetVersionInProjectJSON" (fun _ ->
  !! "./**/project.json"
  |> Seq.iter (DotNetCli.SetVersionInProjectJson release.NugetVersion)
)

// Copies binaries from default VS location to exepcted bin folder
// But keeps a subdirectory structure for each project in the
// src folder to support multiple project outputs
Target "CopyBinaries" (fun _ ->
    !! "src/**/*.??proj"
    |>  Seq.map (fun f -> ((System.IO.Path.GetDirectoryName f) @@ "bin/Release", outDir @@ (System.IO.Path.GetFileNameWithoutExtension f)))
    |>  Seq.iter (fun (fromDir, toDir) -> CopyDir toDir fromDir (fun _ -> true))
)

// --------------------------------------------------------------------------------------
// Clean build results

Target "Clean" (fun _ ->
    CleanDirs [outDir; "temp"]
)

Target "CleanDocs" (fun _ ->
    CleanDirs ["docs/output"]
)

// --------------------------------------------------------------------------------------
// Build library & test project

Target "Build" (fun _ ->
    !! solutionFile
    |> MSBuildReleaseExt "" [ "Platform", "Any CPU" ] "Rebuild"
    |> ignore
)

Target "Build.NETCore" (fun _ ->
  DotNetCli.Restore id

  !! "src/**/project.json"
  |> DotNetCli.Build id
)

Target "RunTests.NETCore" (fun _ ->
  !! "tests/**/project.json"
  |> DotNetCli.Test id
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner

Target "RunTests" (fun _ ->
    !! testAssemblies
    |> Persimmon (fun p ->
      { p with
          ToolPath = findToolInSubPath "Persimmon.Console.exe" (currentDirectory @@ "src" @@ "Persimmon.Console")
          Output = OutputDestination.XmlFile "TestResult.xml"
      }
    )
)

let isAppVeyor = buildServer = AppVeyor

Target "UploadTestResults" (fun _ ->
    let url = sprintf "https://ci.appveyor.com/api/testresults/junit/%s" AppVeyor.AppVeyorEnvironment.JobId
    let files = System.IO.Directory.GetFiles(path = currentDirectory, searchPattern = "*.xml")
    use wc = new System.Net.WebClient()
    files
    |> Seq.iter (fun file ->
        try
            wc.UploadFile(url, file) |> ignore
            printfn "Successfully uploaded test results %s" file
        with
        | ex -> printfn "An error occurred while uploading %s:\r\n%O" file ex
    )
)

#if MONO
#else
// --------------------------------------------------------------------------------------
// SourceLink allows Source Indexing on the PDB generated by the compiler, this allows
// the ability to step through the source code of external libraries https://github.com/ctaggart/SourceLink

Target "SourceLink" (fun _ ->
    let baseUrl = sprintf "%s/%s/{0}/%%var2%%" gitRaw project
    !! "src/**/*.??proj"
    |> Seq.iter (fun projFile ->
        let proj = VsProj.LoadRelease projFile
        SourceLink.Index proj.CompilesNotLinked proj.OutputFilePdb __SOURCE_DIRECTORY__ baseUrl
    )
)

#endif

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "NuGet.Pack" (fun _ ->
  Paket.Pack(fun p ->
    { p with
        OutputPath = "bin"
        Version = release.NugetVersion
        ReleaseNotes = toLines release.Notes})


  let packagingDir = outDir @@ "nuget" @@ "Persimmon"
  [
    "bin/Persimmon/Persimmon.dll"
    "bin/Persimmon/Persimmon.XML"
  ]
  |> CopyFiles (packagingDir @@ "lib" @@ "net20")
  [
    "bin/Persimmon.NET40/Persimmon.dll"
    "bin/Persimmon.NET40/Persimmon.XML"
  ]
  |> CopyFiles (packagingDir @@ "lib" @@ "net40")
  [
    "bin/Persimmon.Portable259/Persimmon.dll"
    "bin/Persimmon.Portable259/Persimmon.XML"
  ]
  |> CopyFiles (packagingDir @@ "lib" @@ "portable-net45+win8+wp8+wpa81+Xamarin.Mac+MonoAndroid10+MonoTouch10+Xamarin.iOS10")

  NuGet (fun p ->
    {
      p with
        OutputPath = outDir
        WorkingDir = packagingDir
        Version = release.NugetVersion
        ReleaseNotes = toLines release.Notes
        DependenciesByFramework =
          [
            {
              FrameworkVersion = "net20"
              Dependencies = []
            }
            {
              FrameworkVersion = "net40"
              Dependencies = []
            }
          ]
    }
  ) "src/Persimmon/Persimmon.nuspec"

  let packagingDir = outDir @@ "nuget" @@ "Persimmon.Runner"
  [
    "bin/Persimmon.Runner/Persimmon.Runner.dll"
    "bin/Persimmon.Runner/Persimmon.Runner.XML"
  ]
  |> CopyFiles (packagingDir @@ "lib" @@ "net35")
  [
    "bin/Persimmon.Runner.NET40/Persimmon.Runner.dll"
    "bin/Persimmon.Runner.NET40/Persimmon.Runner.XML"
  ]
  |> CopyFiles (packagingDir @@ "lib" @@ "net40")

  let dependencies = [
    ("Persimmon", release.NugetVersion)
  ]

  NuGet (fun p ->
    {
      p with
        OutputPath = outDir
        WorkingDir = packagingDir
        Version = release.NugetVersion
        ReleaseNotes = toLines release.Notes
        DependenciesByFramework =
          [
            {
              FrameworkVersion = "net35"
              Dependencies = dependencies
            }
            {
              FrameworkVersion = "net40"
              Dependencies = dependencies
            }
          ]
        FrameworkAssemblies =
          [
            {
              FrameworkVersions = ["net35"]
              AssemblyName = "System.Xml"
            }
            {
              FrameworkVersions = ["net35"]
              AssemblyName = "System.Xml.Linq"
            }
            {
              FrameworkVersions = ["net40"]
              AssemblyName = "System.Xml"
            }
            {
              FrameworkVersions = ["net40"]
              AssemblyName = "System.Xml.Linq"
            }
          ]
    }
  ) "src/Persimmon.Runner/Persimmon.Runner.nuspec"
)

Target "NuGet.AddNetCore" (fun _ ->
  if not isDotnetInstalled then failwith "You need to install .NET core to publish NuGet packages"

  !! "src/**/project.json"
  |> DotNetCli.Pack id

  for proj in ["Persimmon"; "Persimmon.Runner"] do

    let nupkg = sprintf "../../bin/%s.%s.nupkg" proj (release.NugetVersion)
    let netcoreNupkg = sprintf "bin/Release/%s.%s.nupkg" proj (release.NugetVersion)

    let exitCode = Shell.Exec("dotnet", sprintf """mergenupkg --source "%s" --other "%s" --framework netstandard1.6 """ nupkg netcoreNupkg, (sprintf "src/%s/" proj))
    if exitCode <> 0 then failwithf "Command failed with exit code %i" exitCode
)

Target "PublishNuget" (fun _ ->
    Paket.Push(fun p ->
        { p with
            WorkingDir = "bin" })
)

// --------------------------------------------------------------------------------------
// Generate the documentation

Target "GenerateReferenceDocs" (fun _ ->
    if not <| executeFSIWithArgs "docs/tools" "generate.fsx" ["--define:RELEASE"; "--define:REFERENCE"] [] then
      failwith "generating reference documentation failed"
)

let generateHelp' fail debug =
    let args =
        if debug then ["--define:HELP"]
        else ["--define:RELEASE"; "--define:HELP"]
    if executeFSIWithArgs "docs/tools" "generate.fsx" args [] then
        traceImportant "Help generated"
    else
        if fail then
            failwith "generating help documentation failed"
        else
            traceImportant "generating help documentation failed"

let generateHelp fail =
    generateHelp' fail false

Target "GenerateHelp" (fun _ ->
    DeleteFile "docs/content/release-notes.md"
    CopyFile "docs/content/" "RELEASE_NOTES.md"
    Rename "docs/content/release-notes.md" "docs/content/RELEASE_NOTES.md"

    ensureDirectory "docs/files/images"
    CopyFile "docs/files/images/favicon.ico" "paket-files/build/persimmon-projects/Persimmon.Materials/StandardIcons/persimmon.ico"
    CopyFile "docs/files/images/logo.png" "paket-files/build/persimmon-projects/Persimmon.Materials/StandardIcons/persimmon_128.png"

    generateHelp true
)

Target "GenerateHelpDebug" (fun _ ->
    DeleteFile "docs/content/release-notes.md"
    CopyFile "docs/content/" "RELEASE_NOTES.md"
    Rename "docs/content/release-notes.md" "docs/content/RELEASE_NOTES.md"

    generateHelp' true true
)

Target "KeepRunning" (fun _ ->
    use watcher = new FileSystemWatcher(DirectoryInfo("docs/content").FullName,"*.*")
    watcher.EnableRaisingEvents <- true
    watcher.Changed.Add(fun e -> generateHelp false)
    watcher.Created.Add(fun e -> generateHelp false)
    watcher.Renamed.Add(fun e -> generateHelp false)
    watcher.Deleted.Add(fun e -> generateHelp false)

    traceImportant "Waiting for help edits. Press any key to stop."

    System.Console.ReadKey() |> ignore

    watcher.EnableRaisingEvents <- false
    watcher.Dispose()
)

Target "GenerateDocs" DoNothing

let createIndexFsx lang =
    let content = """(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use
// it to define helpers that you do not want to show in the documentation.
#I "../../../bin"
(**
F# Project Scaffold ({0})
=========================
*)
"""
    let targetDir = "docs/content" @@ lang
    let targetFile = targetDir @@ "index.fsx"
    ensureDirectory targetDir
    System.IO.File.WriteAllText(targetFile, System.String.Format(content, lang))

Target "AddLangDocs" (fun _ ->
    let args = System.Environment.GetCommandLineArgs()
    if args.Length < 4 then
        failwith "Language not specified."

    args.[3..]
    |> Seq.iter (fun lang ->
        if lang.Length <> 2 && lang.Length <> 3 then
            failwithf "Language must be 2 or 3 characters (ex. 'de', 'fr', 'ja', 'gsw', etc.): %s" lang

        let templateFileName = "template.cshtml"
        let templateDir = "docs/tools/templates"
        let langTemplateDir = templateDir @@ lang
        let langTemplateFileName = langTemplateDir @@ templateFileName

        if System.IO.File.Exists(langTemplateFileName) then
            failwithf "Documents for specified language '%s' have already been added." lang

        ensureDirectory langTemplateDir
        Copy langTemplateDir [ templateDir @@ templateFileName ]

        createIndexFsx lang)
)

Target "ReleaseDocs" (fun _ ->
    let tempDocsDir = "temp/gh-pages"
    CleanDir tempDocsDir
    Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") "gh-pages" tempDocsDir

    CopyRecursive "docs/output" tempDocsDir true |> tracefn "%A"
    StageAll tempDocsDir
    Git.Commit.Commit tempDocsDir (sprintf "Update generated documentation for version %s" release.NugetVersion)
    Branches.push tempDocsDir
)

#load "paket-files/build/fsharp/FAKE/modules/Octokit/Octokit.fsx"
open Octokit

Target "Release" (fun _ ->
    StageAll ""
    Git.Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    Branches.push ""

    Branches.tag "" release.NugetVersion
    Branches.pushTag "" "origin" release.NugetVersion

    // release on github
    createClient (getBuildParamOrDefault "github-user" "") (getBuildParamOrDefault "github-pw" "")
    |> createDraft gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes
    // TODO: |> uploadFile "PATH_TO_FILE"
    |> releaseDraft
    |> Async.RunSynchronously
)

Target "NETCore" DoNothing

Target "NuGet" DoNothing

Target "All" DoNothing

"Clean"
  ==> "AssemblyInfo"
  ==> "SetVersionInProjectJSON"
  ==> "Build"
  ==> "CopyBinaries"
  ==> "RunTests"
  =?> ("NETCore", isDotnetInstalled)
  =?> ("UploadTestResults",isAppVeyor)
  =?> ("GenerateReferenceDocs",isLocalBuild)
  =?> ("GenerateDocs",isLocalBuild)
  ==> "All"
  =?> ("ReleaseDocs",isLocalBuild)

"Build.NETCore"
  ==> "RunTests.NETCore"
  ==> "NETCore"

"All"
#if MONO
#else
  =?> ("SourceLink", Pdbstr.tryFind().IsSome )
#endif
  ==> "NuGet.Pack"
  ==> "NuGet.AddNetCore"
  ==> "NuGet"

"CleanDocs"
  ==> "GenerateHelp"
  ==> "GenerateReferenceDocs"
  ==> "GenerateDocs"

"CleanDocs"
  ==> "GenerateHelpDebug"

"GenerateHelp"
  ==> "KeepRunning"

"ReleaseDocs"
  ==> "Release"

"NuGet"
  ==> "PublishNuget"
  ==> "Release"

RunTargetOrDefault "All"
