module Docs

open Fake.DotNet

#load ".fake/build.fsx/intellisense.fsx"

open System.IO
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators

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
  ]

let root = website

let bin        = __SOURCE_DIRECTORY__ @@ "bin"
let content    = __SOURCE_DIRECTORY__ @@ "docs/content"
let output     = __SOURCE_DIRECTORY__ @@ "docs/output"
let files      = __SOURCE_DIRECTORY__ @@ "docs/files"
let templates  = __SOURCE_DIRECTORY__ @@ "docs/tools/templates"
let formatting = __SOURCE_DIRECTORY__ @@ "packages/formatting/FSharp.Formatting.CommandTool"
let docTemplate = formatting @@ "templates/docpage.cshtml"

let layoutRootsAll = new System.Collections.Generic.Dictionary<string, string list>()
do
  layoutRootsAll.Add("en", [ templates; formatting @@ "templates"; formatting @@ "templates/reference" ])

  DirectoryInfo.getSubDirectories (DirectoryInfo.ofPath templates)
  |> Seq.iter (fun d ->
   let name = d.Name
   if name.Length = 2 || name.Length = 3 then
     layoutRootsAll.Add(name, [templates @@ name; formatting @@ "templates"; formatting @@ "templates/reference" ]))

let generateHelp() =
  File.delete "docs/content/release-notes.md"
  Shell.copyFile "docs/content/" "RELEASE_NOTES.md"
  Shell.rename "docs/content/release-notes.md" "docs/content/RELEASE_NOTES.md"

  Directory.ensure "docs/files/images"
  Shell.copyFile "docs/files/images/favicon.ico" "paket-files/build/persimmon-projects/Persimmon.Materials/StandardIcons/persimmon.ico"
  Shell.copyFile "docs/files/images/logo.png" "paket-files/build/persimmon-projects/Persimmon.Materials/StandardIcons/persimmon_128.png"

  FSFormatting.createDocs (fun args ->
    { args with
        Source = content
        OutputDirectory = output
        LayoutRoots = layoutRootsAll.["en"]
        ProjectParameters  = ("root", root)::info
        Template = docTemplate
        FsiEval = true } )

  let subdirs = Directory.EnumerateDirectories(content, "*", SearchOption.TopDirectoryOnly)
  for dir in subdirs do
    let dirname = (new DirectoryInfo(dir)).Name
    let layoutRoots =
        let key = layoutRootsAll.Keys |> Seq.tryFind (fun i -> i = dirname)
        match key with
        | Some lang -> layoutRootsAll.[lang]
        | None -> layoutRootsAll.["en"] // "en" is the default language
    let outputDir = output @@ dirname
    Shell.cleanDir outputDir
    FSFormatting.createDocs (fun args ->
      { args with
          Source = content @@ dirname
          OutputDirectory = output @@ dirname
          LayoutRoots = layoutRoots
          ProjectParameters  = ("root", root)::info
          Template = docTemplate
          FsiEval = true } )