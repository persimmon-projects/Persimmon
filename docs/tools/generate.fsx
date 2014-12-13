let referenceBinaries = [ "Persimmon.dll" ]
let website = "/Persimmon"
let githubLink = "https://github.com/persimmon-projects/Persimmon"
let info =
  [ "project-name", "Persimmon"
    "project-author", "persimmon-projects"
    "project-summary", "A unit test framework for F# using computation expressions."
    "project-github", githubLink
    "project-nuget", "https://www.nuget.org/packages/Persimmon/"]

#I "../../packages/FSharp.Formatting/lib/net40"
#I "../../packages/RazorEngine/lib/net40"
#I "../../packages/FSharp.Compiler.Service/lib/net40"
#r "../../packages/Microsoft.AspNet.Razor/lib/net40/System.Web.Razor.dll"
#r "RazorEngine.dll"
#r "FSharp.Compiler.Service.dll"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.MetadataFormat.dll"
#r "FSharp.MarkDown.dll"
open System.IO
open FSharp.Literate
open FSharp.MetadataFormat

let (@@) path1 path2 = Path.Combine(path1, path2)

#if RELEASE
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
  for lib in referenceBinaries do
    MetadataFormat.Generate(
      bin @@ lib,
      outputDir,
      layoutRoots,
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

