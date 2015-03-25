let referenceBinaries = [ "Persimmon.dll"; "Persimmon.Runner.dll"; "Persimmon.Script.dll" ]
let website = "/Persimmon"
let githubLink = "https://github.com/persimmon-projects/Persimmon"
let nugetLink = "https://www.nuget.org/packages/Persimmon"
let info =
  [ "project-name", "Persimmon"
    "project-author", "persimmon-projects"
    "project-summary", "A unit test framework for F# using computation expressions."
    "project-github", githubLink
    "project-nuget", nugetLink + "/"
    "console-project-nuget", nugetLink + ".Console/"
    "runner-project-nuget", nugetLink + ".Runner/"
    "script-project-nuget", nugetLink + ".Script/"
  ]

#I "../../packages/FSharp.Formatting/lib/net40"
#I "../../packages/FSharp.Compiler.Service/lib/net40"
#I "../../packages/FSharpVSPowerTools.Core/lib/net45"
#r "FSharpVSPowerTools.Core.dll"
#r "FSharp.Compiler.Service.dll"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.MetadataFormat.dll"
#r "FSharp.MarkDown.dll"
open System.IO
open FSharp.Literate
open FSharp.MetadataFormat

let (@@) path1 path2 = Path.Combine(path1, path2)

#if Release
let root = website
let configuration = "Release"
#else
let root = "file://" + (__SOURCE_DIRECTORY__ @@ "../output")
let configuration = "Debug"
#endif

let bin = __SOURCE_DIRECTORY__ @@ "../../Persimmon.Console/bin" @@ configuration
let content = __SOURCE_DIRECTORY__ @@ "../content"
let output = __SOURCE_DIRECTORY__ @@ "../output"
let files = __SOURCE_DIRECTORY__ @@ "../files"
let templates = __SOURCE_DIRECTORY__ @@ "templates"
let formatting = __SOURCE_DIRECTORY__ @@ "../../packages/FSharp.Formatting/"
let docTemplate = formatting @@ "templates/docpage.cshtml"

let layoutRoots = [
  templates
  formatting @@ "templates"
  formatting @@ "templates/reference"
]

let buildReference () =
  let outputDir = output @@ "reference"
  System.IO.Directory.CreateDirectory(outputDir) |> ignore
  MetadataFormat.Generate(
    referenceBinaries |> List.map (fun lib -> __SOURCE_DIRECTORY__ @@  "../.." @@ Path.GetFileNameWithoutExtension(lib) @@ "bin" @@ configuration @@ lib),
    outputDir,
    layoutRoots,
    otherFlags = [
      "-r:System"
      "-r:System.Core"
      "-r:System.Linq"
    ],
    parameters = ("root", root)::info,
    sourceRepo = githubLink @@ "tree/master",
    sourceFolder = __SOURCE_DIRECTORY__ @@ ".." @@ "..",
    publicOnly = true)

let buildDocumentation () =
  let subdirs = Directory.EnumerateDirectories(content, "*", SearchOption.AllDirectories)
  for dir in Seq.append [content] subdirs do
    let sub = if dir.Length > content.Length then dir.Substring(content.Length + 1) else "."
    Literate.ProcessDirectory
      (dir, docTemplate, output @@ sub, replacements = ("root", root)::info,
        layoutRoots = layoutRoots,  fsiEvaluator = new FsiEvaluator(), lineNumbers=false)

buildDocumentation ()
buildReference ()

