open System
open System.IO
open System.Text
open System.Diagnostics
open System.Reflection

open Persimmon
open Persimmon.Runner
open Persimmon.Output
open System.Security.Policy

let createAppDomain (applicationBasePath: string) (assembly: FileInfo) =
  let contextId = Guid.NewGuid()
  let separatedAppDomainName = sprintf "persimmon console domain - %s" (contextId.ToString()) 
  let shadowCopyTargets = String.concat ";" [ assembly.Directory.FullName ;applicationBasePath ]
  let separatedAppDomainSetup =
    AppDomainSetup(
      ApplicationName = separatedAppDomainName,
      ApplicationBase = applicationBasePath,
      ShadowCopyFiles = "false",
      ShadowCopyDirectories = shadowCopyTargets)
  let configurationFilePath = assembly.FullName + ".config"
  if File.Exists(configurationFilePath) then
    separatedAppDomainSetup.ConfigurationFile <- configurationFilePath

  let separatedAppDomainEvidence = Evidence(AppDomain.CurrentDomain.Evidence)

  AppDomain.CreateDomain(separatedAppDomainName, separatedAppDomainEvidence, separatedAppDomainSetup)

let runAndReport (args: Args) (watch: Stopwatch) (reporter: Reporter) (founds: FileInfo list)  =
  let appDomains = ResizeArray<_>()
  try
    let mutable errors = 0
    let results = ResizeArray<_>()

    watch.Start()

    // collect and run
    let currentSetUp = AppDomain.CurrentDomain.SetupInformation
    let applicationBasePath = currentSetUp.ApplicationBase
    for assembly in founds do
      let appDomain = createAppDomain applicationBasePath assembly
      appDomains.Add(appDomain)

      let proxy = PersimmonProxy.Create(appDomain)
      let testResults = proxy.CollectAndRun(assembly.FullName, args)
      do
        errors <- errors + testResults.Errors
        results.AddRange(testResults.Results)

    watch.Stop()

    // report
    reporter.ReportProgress(TestResult.endMarker)
    reporter.ReportSummary(results)
    errors
  finally
    appDomains |> Seq.iter AppDomain.Unload

let run (args: Args) =
  let watch = Stopwatch()
  
  let requireFileName = Args.requireFileName args
  use reporter = Args.reporter watch args

  let founds, notFounds = args.Inputs |> List.partition (fun file -> file.Exists)
  if founds |> List.isEmpty then
    reporter.ReportError("input is empty.")
    -1
  elif requireFileName && Option.isNone args.Output then
    reporter.ReportError("xml format option require 'output' option.")
    -2
  elif notFounds |> List.isEmpty then
    try
      runAndReport args watch reporter founds
    with e ->
      reporter.ReportError("!!! FATAL Error !!!")
      reporter.ReportError(e.ToString())
      if e.InnerException <> null then
        reporter.ReportError("InnerException:")
        reporter.ReportError(e.InnerException.ToString())
      -100
  else
    reporter.ReportError("file not found: " + (String.Join(", ", notFounds)))
    -2

[<EntryPoint>]
let main argv =
  let args = Args.parse Args.empty (argv |> Array.toList)
  run args
