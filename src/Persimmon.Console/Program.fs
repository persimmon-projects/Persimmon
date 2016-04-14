open System
open System.IO
open System.Text
open System.Diagnostics
open System.Reflection

open Persimmon
open Persimmon.Runner
open Persimmon.Output

let entryPoint (args: Args) =
  let watch = Stopwatch()
  use progress = if args.NoProgress then IO.TextWriter.Null else Console.Out
  let runAndReport: (Reporter -> TestMetadata list -> int) =
    if args.Parallel then
      fun reporter tests ->
        async {
          watch.Start()
          let! res = TestRunner.asyncRunAllTests reporter.ReportProgress tests
          watch.Stop()
          // report
          reporter.ReportProgress(TestResult.endMarker)
          reporter.ReportSummary(res.Results)
          return res.Errors
        }
        |> Async.RunSynchronously
    else
      fun reporter tests ->
        watch.Start()
        let res = TestRunner.runAllTests reporter.ReportProgress tests
        watch.Stop()
        // report
        reporter.ReportProgress(TestResult.endMarker)
        reporter.ReportSummary(res.Results)
        res.Errors
  let requireFileName, outputs =
    let console = {
      Writer = Console.Out
      Formatter = Formatter.SummaryFormatter.normal watch
    }
    match args.Output, args.Format with
    | (Some file, JUnitStyleXml) ->
      let xml = {
        Writer = new StreamWriter(file.FullName, false, Encoding.UTF8) :> TextWriter
        Formatter = Formatter.XmlFormatter.junitStyle watch
      }
      (true, [console; xml])
    | (Some file, Normal) ->
      let file = {
        Writer = new StreamWriter(file.FullName, false, Encoding.UTF8) :> TextWriter
        Formatter = console.Formatter
      }
      (false, [console; file])
    | (None, Normal) -> (false, [console])
    | (None, JUnitStyleXml) -> (true, [])
  use error =
    match args.Error with
    | Some file -> new StreamWriter(file.FullName, false, Encoding.UTF8) :> TextWriter
    | None -> Console.Error

  use reporter =
    new Reporter(
      new Printer<_>(progress, Formatter.ProgressFormatter.dot),
      new Printer<_>(outputs),
      new Printer<_>(error, Formatter.ErrorFormatter.normal))

  if args.Help then
    error.WriteLine(Args.help)

  let founds, notFounds = args.Inputs |> List.partition (fun file -> file.Exists)
  if founds |> List.isEmpty then
    reporter.ReportError("input is empty.")
    -1
  elif requireFileName && Option.isNone args.Output then
    reporter.ReportError("xml format option require 'output' option.")
    -2
  elif notFounds |> List.isEmpty then
    let asms = founds |> List.map (fun f ->
      let assemblyRef = AssemblyName.GetAssemblyName(f.FullName)
      Assembly.Load(assemblyRef))
    // collect and run
    let tests = TestCollector.collectRootTestObjects asms
    runAndReport reporter tests
  else
    reporter.ReportError("file not found: " + (String.Join(", ", notFounds)))
    -2

type FailedCounter () =
  inherit MarshalByRefObject()
  
  member val Failed = 0 with get, set

[<Serializable>]
type Callback (args: Args, body: Args -> int, failed: FailedCounter) =
  member __.Run() =
    failed.Failed <- body args

let run act =
  let info = AppDomain.CurrentDomain.SetupInformation
  let appDomain = AppDomain.CreateDomain("persimmon console domain", null, info)
  try
    appDomain.DoCallBack(act)
  finally
    AppDomain.Unload(appDomain)

[<EntryPoint>]
let main argv = 
  let args = Args.parse Args.empty (argv |> Array.toList)
  let failed = FailedCounter()
  let callback = Callback(args, entryPoint, failed)
  run (CrossAppDomainDelegate(callback.Run))
  failed.Failed
