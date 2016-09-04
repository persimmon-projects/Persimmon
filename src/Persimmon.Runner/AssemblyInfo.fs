namespace System
open System.Reflection
open System.Runtime.InteropServices

[<assembly: AssemblyTitleAttribute("Persimmon.Runner")>]
[<assembly: AssemblyDescriptionAttribute("")>]
[<assembly: GuidAttribute("EB676E7D-9D9D-47C2-A6DC-173B536641B1")>]
[<assembly: AssemblyProductAttribute("Persimmon")>]
[<assembly: AssemblyVersionAttribute("1.2.0")>]
[<assembly: AssemblyFileVersionAttribute("1.2.0")>]
[<assembly: AssemblyInformationalVersionAttribute("1.2.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.2.0"
