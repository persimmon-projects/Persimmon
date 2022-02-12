open System
open System.IO
open Persimmon
open Persimmon.Internals
open Persimmon.Console
open System.Security.Policy
open Persimmon.Console.RunnerStrategy

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

type RunInAppDomainStrategy(inputs: FileInfo list) =
  let appDomains = ResizeArray<_>()

  interface IRunnerStrategy with
    member this.CreateTestContext(): seq<ITestContext> = seq {
      for assembly in inputs do
        let appDomain = createAppDomain(assembly)
        appDomains.Add(appDomain)

        let t = typeof<TestManager>
        let proxy = appDomain.CreateInstanceAndUnwrap(t.Assembly.FullName, t.FullName) :?> TestManager
        yield {
          new ITestContext with
            member _.Callback with set(value) = proxy.Callback <- value
            member _.Parallel with set(value) = proxy.Parallel <- value
            member _.Filter with set(value) = proxy.Filter <- value

            member _.Collect() = proxy.Collect(assembly.FullName)
            member _.Run() = proxy.Run()
        }
    }

  interface IDisposable with
    member this.Dispose(): unit = appDomains |> Seq.iter AppDomain.Unload

[<EntryPoint>]
let main argv =
  let args = Args.parse Args.empty (argv |> Array.toList)
  use strategy = new RunInAppDomainStrategy(args.Inputs)
  Runner.run args strategy