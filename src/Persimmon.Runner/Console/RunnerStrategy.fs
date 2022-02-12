namespace Persimmon.Console

open System.IO
open Persimmon.Internals

type IRunnerStrategy =
  abstract CreateTestManager: assembly:FileInfo -> TestManager