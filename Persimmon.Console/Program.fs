open System

let entryPoint (args: Args) =
  use output = new Writer(args.Output, Console.Out)
  use error = new Writer(args.Error, Console.Error)

  let founds, notFounds = args.Inputs |> List.partition (fun file -> file.Exists)
  if founds |> List.isEmpty then
    error.WriteLine("input is empty.")
    -1
  elif notFounds |> List.isEmpty then
    Runner.runAllTests (output, error) founds
  else
    error.WriteLine("file not found: " + (String.Join(", ", notFounds)))
    -2

type FailedCounter () =
  inherit MarshalByRefObject()
  
  member val Failed = 0 with get, set

[<Serializable>]
type Callback (args: Args, body: Args -> int, failed: FailedCounter) =
  member this.Run() =
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
