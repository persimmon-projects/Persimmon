#r @"..\Persimmon\bin\Debug\Persimmon.dll"
#r @"..\Persimmon.Script\bin\Debug\Persimmon.Script.dll"

open Persimmon
open UseTestNameByReflection

ScriptRunner.run begin fun ctx ->
  [
    ctx.test "unit" {
      do! assertEquals 1 1
    }
    ctx.test "return value" {
      return 1
    }
  ]
end

let parameterizeTest x = test {
  do! assertEquals 0 (x % 2)
}

ScriptRunner.run begin fun ctx ->
  ctx.parameterize {
    case 2
    case 4
    run parameterizeTest
  }
end

