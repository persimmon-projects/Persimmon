(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use
// it to define helpers that you do not want to show in the documentation.
#I "../../Persimmon/bin/Release"
#I "../../Persimmon.Runner/bin/Release"
#r "Persimmon"

(**
<div class="blog-post">

Getting Started
===============

An overview of Persimmon, how to download and use.

Creating project(or Getting sample project)
-------------------------------------------

Now create a project and install Persimmon from NuGet (and enable NuGet Package Restore), or download [sample project](https://github.com/persimmon-projects/Persimmon.Demo).

Getting Persimmon console runner
--------------------------------

Run the command below:

    [lang=powershell]
    .\.nuget\NuGet.exe Install Persimmon.Console -OutputDirectory tools -ExcludeVersion

The first step
--------------

You can write the tests by using ``test`` computation expression and assertion functions.

*)

open Persimmon

let ``some variable name`` = test "first test example" {
    do! assertEquals 0 (4 % 2)
}

(**

Executing test
--------------

Run the command below:

    [lang=powershell]
    .\tools\Persimmon.Console\tools\Persimmon.Console.exe 'input file path'

Testing exceptions
------------------

``trap`` computation expression can catch exceptions.

*)

exception MyException

let ``exception test`` = test "exn test" {
  let f () =
    raise MyException
    42
  let! e = trap { it (f ()) }
  do! assertEquals "" e.Message
  do! assertEquals typeof<MyException> (e.GetType())
  do! assertEquals "" (e.StackTrace.Substring(0, 5))
}

(**

Parameterized tests
-------------------

Persimmon supports parameterized tests.

*)

let ``case parameterize test`` =
  let parameterizeTest (x, y) = test "case parameterize test" {
    do! assertEquals x y
  }
  parameterize {
    case (1, 1)
    case (1, 2)
    run parameterizeTest
  }

let inputs = [ 2 .. 2 .. 20 ]

let ``source parameterize test`` =
  let parameterizeTest x = test "source parameterize test" {
    do! assertEquals 0 (x % 2)
  }
  parameterize {
    source inputs
    run parameterizeTest
  }

(**

Omitting test name
------------------

Open ``UseTestNameByReflection`` module:

*)

open UseTestNameByReflection

(**

</div>
*)
