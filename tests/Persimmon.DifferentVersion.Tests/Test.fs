module Persimmon.FS43.Tests

open Persimmon
open Persimmon.Syntax.UseTestNameByReflection

let fsharpCoreVersionTest = test {
  let version = typedefof<option<_>>.Assembly.GetName().Version.ToString()
  do! version |> assertEquals "4.4.3.0"
}