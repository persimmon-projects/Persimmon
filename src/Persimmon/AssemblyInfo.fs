namespace System
open System.Reflection
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

[<assembly: AssemblyTitleAttribute("Persimmon")>]
[<assembly: AssemblyDescriptionAttribute("")>]
[<assembly: InternalsVisibleToAttribute("Persimmon.Tests")>]
[<assembly: GuidAttribute("F5EB6EEA-FA93-4F0D-9C23-60A91DB012DB")>]
[<assembly: AssemblyProductAttribute("Persimmon")>]
[<assembly: AssemblyVersionAttribute("1.2.0")>]
[<assembly: AssemblyFileVersionAttribute("1.2.0")>]
[<assembly: AssemblyInformationalVersionAttribute("1.2.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.2.0"
