module Persimmon.FS60.Tests

open Persimmon
open Persimmon.Syntax.UseTestNameByReflection

let fsharpCoreVersionTest = test {
  let version = typedefof<option<_>>.Assembly.GetName().Version.ToString()
  do! version |> assertEquals "5.0.0.0"
}