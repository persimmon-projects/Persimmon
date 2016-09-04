namespace System
open System.Reflection
open System.Runtime.InteropServices

[<assembly: AssemblyTitleAttribute("Persimmon.Script")>]
[<assembly: AssemblyDescriptionAttribute("")>]
[<assembly: GuidAttribute("8B733755-9708-4F9C-A356-AE0C2EF1680D")>]
[<assembly: AssemblyProductAttribute("Persimmon")>]
[<assembly: AssemblyVersionAttribute("1.2.0")>]
[<assembly: AssemblyFileVersionAttribute("1.2.0")>]
[<assembly: AssemblyInformationalVersionAttribute("1.2.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.2.0"
