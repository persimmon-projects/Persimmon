open Persimmon.Console
open System.Reflection


[<EntryPoint>]
let main argv =
  let args = Args.parse Args.empty (argv |> Array.toList)
  Runner.runTestsInAssembly args [ Assembly.GetExecutingAssembly() ]