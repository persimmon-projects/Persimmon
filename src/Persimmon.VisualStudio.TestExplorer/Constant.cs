using System;

namespace Persimmon.VisualStudio.TestExplorer
{
    internal static class Constant
    {
        public const string ExtensionUriString = "executor://persimmon.visualstudio.testexplorer";
        public static readonly Uri ExtensionUri = new Uri(ExtensionUriString);
    }
}
