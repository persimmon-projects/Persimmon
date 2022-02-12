open System
open System.IO
open Persimmon
open Persimmon.Internals
open Persimmon.Console
open System.Security.Policy

let createAppDomain (assembly: FileInfo) =
  let contextId = Guid.NewGuid()
  let separatedAppDomainName = sprintf "persimmon console domain - %s" (contextId.ToString()) 
  let shadowCopyTargets = assembly.Directory.FullName
  let separatedAppDomainSetup =
    AppDomainSetup(
      ApplicationName = separatedAppDomainName,
      ApplicationBase = assembly.Directory.FullName,
      ShadowCopyFiles = "false",
      ShadowCopyDirectories = shadowCopyTargets)
  let configurationFilePath = assembly.FullName + ".config"
  if File.Exists(configurationFilePath) then
    separatedAppDomainSetup.ConfigurationFile <- configurationFilePath

  let separatedAppDomainEvidence = Evidence(AppDomain.CurrentDomain.Evidence)

  AppDomain.CreateDomain(separatedAppDomainName, separatedAppDomainEvidence, separatedAppDomainSetup)

type RunInAppDomainStrategy() =
  let appDomains = ResizeArray<_>()

  interface IRunnerStrategy with
    member this.CreateTestManager(assembly: FileInfo): TestManager = 
      let appDomain = createAppDomain(assembly)
      appDomains.Add(appDomain)

      let t = typeof<TestManager>
      let proxy = appDomain.CreateInstanceAndUnwrap(t.Assembly.FullName, t.FullName) :?> TestManager
      proxy

  interface IDisposable with
    member this.Dispose(): unit = appDomains |> Seq.iter AppDomain.Unload

type TestManagerCallback(progress: TestResult -> unit) =
  inherit MarshalByRefObject()

  interface ITestManagerCallback with
    member this.Progress(testResult) = progress testResult


[<EntryPoint>]
let main argv =
  use strategy = new RunInAppDomainStrategy()
  let args = Args.parse Args.empty (argv |> Array.toList)
  Runner.run strategy args