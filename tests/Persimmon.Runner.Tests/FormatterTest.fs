module Persimmon.Runner.Tests.FormatterTest

open System
open System.IO
open System.Xml
open System.Xml.Schema
open System.Xml.Linq
open System.Diagnostics
open Persimmon
open UseTestNameByReflection
open Persimmon.Output

module Xml =

  let xsd = Path.Combine(__SOURCE_DIRECTORY__, @"..\..\paket-files\build\bluebird75\luaunit\junitxml\junit-jenkins.xsd")
  let outFile uid = Path.Combine(__SOURCE_DIRECTORY__, @"..\..\temp\" + uid + ".xml")

  let validate tests = test {
    use reader = new StreamReader(xsd)
    let schemas = XmlSchemaSet()
    schemas.Add("", XmlReader.Create(reader)) |> ignore
    let watch = Stopwatch()
    let formatter = Formatter.XmlFormatter.junitStyle watch
    let filePath = Guid.NewGuid().ToString() |> outFile
    let writer = new StreamWriter(filePath)
    formatter.Format(
      Internals.TestRunnerImpl.runTests ignore Internals.TestRunnerImpl.asyncSequential tests |> Async.RunSynchronously
    ).WriteTo(writer)
    writer.Dispose()
    let doc = XDocument.Load(filePath)
    let mutable ex: (exn * string) option = None
    doc.Validate(schemas, ValidationEventHandler(fun _ e ->
      ex <- Some((e.Exception :> exn, e.Message)
    )))
    match ex with
    | Some(e, msg) ->
      do! fail msg
      return raise e
    | None -> return ()
  }

  let ``should validate result xml`` = parameterize {
    source [
      [
        Context("FormatterTest", [TestCase.makeDone (Some "testcase0") [] (Passed ())])
      ]
      [
        Context("FormatterTest", [TestCase.makeDone (Some "testcase0") [] (NotPassed(None, Skipped "skip test"))])
      ]
      [
        Context("FormatterTest", [TestCase.makeDone (Some "testcase0") [] (NotPassed(None, Violated "fail test"))])
      ]
      [
        Context("FormatterTest", [TestCase.makeError (Some "testcase0") [] (exn("test"))])
      ]
    ]
    run validate
  }
