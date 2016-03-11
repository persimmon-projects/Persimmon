using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Persimmon.VisualStudio.TestRunner.Internals
{
    public sealed class AssemblyInjector : MarshalByRefObject
    {
        private readonly Dictionary<string, AssemblyName> loadedAssemblies_;

        public AssemblyInjector(AssemblyName[] names)
        {
            Debug.Assert(names != null);

            loadedAssemblies_ = names.ToDictionary(name => name.FullName);

            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
        }

        private Assembly AssemblyResolve(object sender, ResolveEventArgs e)
        {
            Debug.WriteLine(string.Format(
                "AssemblyResolve: Name={0}, Requesting={1}, Current={2}",
                e.Name,
                e.RequestingAssembly,
                AppDomain.CurrentDomain));

            AssemblyName name;
            lock (loadedAssemblies_)
            {
                if (loadedAssemblies_.TryGetValue(e.Name, out name) == false)
                {
                    return null;
                }
            }

            var assembly = Assembly.Load(name);
            return assembly;
        }
    }
}
