[<AutoOpen>]
module Persimmon.Syntax

let context name children = Context(name, children)

let test name = TestBuilder(name)
let parameterize = ParameterizeBuilder()
let trap = TrapBuilder()
