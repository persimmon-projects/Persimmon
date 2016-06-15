namespace System
open System.Reflection
open System.Runtime.InteropServices

[<assembly: AssemblyTitleAttribute("Persimmon")>]
[<assembly: AssemblyDescriptionAttribute("")>]
[<assembly: GuidAttribute("F5EB6EEA-FA93-4F0D-9C23-60A91DB012DB")>]
[<assembly: AssemblyProductAttribute("Persimmon")>]
[<assembly: AssemblyVersionAttribute("1.1.0")>]
[<assembly: AssemblyFileVersionAttribute("1.1.0")>]
[<assembly: AssemblyInformationalVersionAttribute("1.1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.1.0"
