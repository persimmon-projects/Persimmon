open System

let entryPoint (args: Args) =
  use progress = new Writer(if args.NoProgress then IO.TextWriter.Null else Console.Out)
  use output = new Writer(args.Output, Console.Out)
  use error = new Writer(args.Error, Console.Error)

  use reporter =
    new Reporter(
      new Printer<_>(progress, Formatters.ProgressFormatter.dot),
      new Printer<_>(output, Formatters.SummaryFormatter.normal),
      new Printer<_>(error, Formatters.ErrorFormatter.normal))

  try
    if args.Help then
      error.WriteLine(Args.help)

    let founds, notFounds = args.Inputs |> List.partition (fun file -> file.Exists)
    if founds |> List.isEmpty then
      reporter.ReportError("input is empty.")
      -1
    elif notFounds |> List.isEmpty then
      Runner.runAllTests reporter founds
    else
      reporter.ReportError("file not found: " + (String.Join(", ", notFounds)))
      -2
  finally
    progress.WriteLine("")
    reporter.ReportSummary()

type FailedCounter () =
  inherit MarshalByRefObject()
  
  member val Failed = 0 with get, set

[<Serializable>]
type Callback (args: Args, body: Args -> int, failed: FailedCounter) =
  member __.Run() =
    failed.Failed <- body args

let run act =
  let info = AppDomain.CurrentDomain.SetupInformation
  let appDomain = AppDomain.CreateDomain("persimmon test domain", null, info)
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
