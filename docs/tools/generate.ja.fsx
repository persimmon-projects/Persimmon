let referenceBinaries = [ "Persimmon.dll" ]
let website = "/Persimmon/ja"
let info =
  [ "project-name", "Persimmon"
    "project-author", "persimmon-projects"
    "project-summary", "コンピュテーションを用いた、 F# のためのユニットテスティングフレームワークです。"
    "project-github", "https://github.com/persimmon-projects/Persimmon"
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

#if Release
let root = website
#else
let root = "file://" + (__SOURCE_DIRECTORY__ @@ "../output/ja")
#endif

let bin = __SOURCE_DIRECTORY__ @@ "../../bin"
let content = __SOURCE_DIRECTORY__ @@ "../content/ja"
let output = __SOURCE_DIRECTORY__ @@ "../output/ja"
let files = __SOURCE_DIRECTORY__ @@ "../files"
let templates = __SOURCE_DIRECTORY__ @@ "templates/ja"
let formatting = __SOURCE_DIRECTORY__ @@ "../../packages/FSharp.Formatting/"
let docTemplate = formatting @@ "templates/docpage.cshtml"

let layoutRoots = [
  templates
  formatting @@ "templates"
  formatting @@ "templates/reference"
]

let rec copyDir sourceDirName outputDirName =
  let dir = DirectoryInfo(sourceDirName)
  let dirs = dir.GetDirectories()

  if not <| Directory.Exists(outputDirName) then
    Directory.CreateDirectory(outputDirName) |> ignore

  let files = dir.GetFiles()
  for file in files do
    let path = outputDirName @@ file.Name
    file.CopyTo(path, false) |> ignore

  for subdir in dirs do
    outputDirName @@ subdir.Name |> copyDir subdir.FullName

let buildDocumentation () =
  let subdirs =
    Directory.EnumerateDirectories(content, "*", SearchOption.AllDirectories)
    |> Seq.filter (fun x -> x.Contains "ja")
  for dir in Seq.append [content] subdirs do
    let sub = if dir.Length > content.Length then dir.Substring(content.Length + 1) else "."
    Literate.ProcessDirectory
      (dir, docTemplate, output @@ sub, replacements = ("root", root)::info,
        layoutRoots = layoutRoots, fsiEvaluator = new FsiEvaluator(), lineNumbers=false)

copyDir (formatting @@ "styles") (output @@ "content")
buildDocumentation ()

