using System.Reflection;

[assembly: AssemblyCompany("persimmon-projects")]
[assembly: AssemblyProduct("Persimmon")]
[assembly: AssemblyCopyright("Copyright (c) 2016 persimmon-projects")]
[assembly: AssemblyTrademark("Persimmon")]

#if DEBUG
[assembly: AssemblyConfiguration("DEBUG")]
#else
[assembly: AssemblyConfiguration("RELEASE")]
#endif
