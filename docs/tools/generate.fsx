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

#I "../../packages/FAKE/tools/"
#load "../../packages/FSharp.Formatting/FSharp.Formatting.fsx"
#r "NuGet.Core.dll"
#r "FakeLib.dll"
open Fake
open System.IO
open Fake.FileHelper
open FSharp.Literate
open FSharp.MetadataFormat

#if RELEASE
let root = website
#else
let root = "file://" + (__SOURCE_DIRECTORY__ @@ "../output")
#endif

let bin        = __SOURCE_DIRECTORY__ @@ "../../bin"
let content    = __SOURCE_DIRECTORY__ @@ "../content"
let output     = __SOURCE_DIRECTORY__ @@ "../output"
let files      = __SOURCE_DIRECTORY__ @@ "../files"
let templates  = __SOURCE_DIRECTORY__ @@ "templates"
let formatting = __SOURCE_DIRECTORY__ @@ "../../packages/FSharp.Formatting/"
let docTemplate = formatting @@ "templates/docpage.cshtml"

let layoutRootsAll = new System.Collections.Generic.Dictionary<string, string list>()
layoutRootsAll.Add("en",[ templates; formatting @@ "templates"
                          formatting @@ "templates/reference" ])
subDirectories (directoryInfo templates)
|> Seq.iter (fun d ->
                let name = d.Name
                if name.Length = 2 || name.Length = 3 then
                    layoutRootsAll.Add(
                            name, [templates @@ name
                                   formatting @@ "templates"
                                   formatting @@ "templates/reference" ]))

let copyFiles () =
  CopyRecursive files output true |> Log "Copying file: "
  ensureDirectory (output @@ "content")
  CopyRecursive (formatting @@ "styles") (output @@ "content") true
    |> Log "Copying styles and scripts: "

let references =
  if isMono then
    let d = RazorEngine.Compilation.ReferenceResolver.UseCurrentAssembliesReferenceResolver()
    let loadedList = d.GetReferences () |> Seq.map (fun r -> r.GetFile()) |> Seq.cache
    let getItem name = loadedList |> Seq.find (fun l -> l.Contains name)
    [ (getItem "FSharp.Core").Replace("4.3.0.0", "4.3.1.0")
      Path.GetFullPath "./../../packages/FSharp.Compiler.Service/lib/net40/FSharp.Compiler.Service.dll"
      Path.GetFullPath "./../../packages/FSharp.Formatting/lib/net40/System.Web.Razor.dll"
      Path.GetFullPath "./../../packages/FSharp.Formatting/lib/net40/RazorEngine.dll"
      Path.GetFullPath "./../../packages/FSharp.Formatting/lib/net40/FSharp.Literate.dll"
      Path.GetFullPath "./../../packages/FSharp.Formatting/lib/net40/FSharp.CodeFormat.dll"
      Path.GetFullPath "./../../packages/FSharp.Formatting/lib/net40/FSharp.MetadataFormat.dll" ]
    |> Some
  else None

let binaries =
    directoryInfo bin
    |> subDirectories
    |> Array.choose (fun d -> if d.Name <> "Persimmon.Console" then Some(d.FullName @@ (sprintf "%s.dll" d.Name)) else None)
    |> List.ofArray

let libDirs =
    directoryInfo bin
    |> subDirectories
    |> Array.map (fun d -> d.FullName)
    |> List.ofArray

let buildReference () =
  CleanDir (output @@ "reference")
  MetadataFormat.Generate
    ( binaries, output @@ "reference", layoutRootsAll.["en"],
      parameters = ("root", root)::info,
      sourceRepo = githubLink @@ "tree/master",
      sourceFolder = __SOURCE_DIRECTORY__ @@ ".." @@ "..",
      ?assemblyReferences = references,
      publicOnly = true,libDirs = libDirs )

let buildDocumentation () =
  let fsi = FsiEvaluator()
  Literate.ProcessDirectory
    ( content, docTemplate, output, replacements = ("root", root)::info,
      layoutRoots = layoutRootsAll.["en"],
      ?assemblyReferences = references,
      generateAnchors = true,
      processRecursive = false,
      fsiEvaluator = fsi )

  let subdirs = Directory.EnumerateDirectories(content, "*", SearchOption.TopDirectoryOnly)
  for dir in subdirs do
    let dirname = (new DirectoryInfo(dir)).Name
    let layoutRoots =
        let key = layoutRootsAll.Keys |> Seq.tryFind (fun i -> i = dirname)
        match key with
        | Some lang -> layoutRootsAll.[lang]
        | None -> layoutRootsAll.["en"] // "en" is the default language
    Literate.ProcessDirectory
      ( dir, docTemplate, output @@ dirname, replacements = ("root", root)::info,
        layoutRoots = layoutRoots,
        ?assemblyReferences = references,
        generateAnchors = true,
        fsiEvaluator = fsi )

copyFiles()
#if HELP
buildDocumentation()
#endif
#if REFERENCE
buildReference()
#endif
