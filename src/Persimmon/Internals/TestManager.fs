namespace Persimmon.Internals

open System
open Persimmon
open System.Reflection


type ITestManagerCallback =
  abstract Progress : testResult:TestResult -> unit

/// The proxy for cross application domain.
/// Initialize properties before calling Collect and Run.
type TestManager() =
  inherit MarshalByRefObject()

  let mutable tests = Seq.empty

  let emptyCallback =
    { new ITestManagerCallback with
        member this.Progress(_) = ()
    }

  member val Parallel = false with get, set
  member val Filter = TestFilter.allPass with get, set
  member val Callback = emptyCallback with get, set

  member this.Collect(assemblyPath: string) : unit =
    let asm = Assembly.LoadFrom(assemblyPath)
    let found = TestCollector().Collect(asm)
    do tests <- Seq.append tests found

  member this.Run() : RunResult<ResultNode> =
    let filter = TestFilter.make this.Filter
    let progress x = this.Callback.Progress x

    if this.Parallel then
      TestRunner().AsyncRunAllTests(progress, filter, tests)
      |> Async.RunSynchronously
    else
      TestRunner().RunSynchronouslyAllTests(progress, filter, tests)