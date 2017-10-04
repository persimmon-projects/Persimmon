namespace global

open System
open System.Reflection
open Persimmon
open Persimmon.Runner

type PersimmonProxy() =
  inherit MarshalByRefObject()

  static member Create(appDomain: AppDomain) =
    let t = typeof<PersimmonProxy>
    appDomain.CreateInstanceAndUnwrap(t.Assembly.FullName, t.FullName) :?> PersimmonProxy

  member this.CollectAndRun(assemblyPath: string, args: Args) =
    let asm = Assembly.LoadFrom(assemblyPath)
    let tests = TestCollector.collectRootTestObjects [ asm ]
    use progress = Args.progressPrinter args
    if args.Parallel then
      TestRunner.asyncRunAllTests progress.Print TestFilter.allPass tests
      |> Async.RunSynchronously
    else
      TestRunner.runAllTests progress.Print TestFilter.allPass tests