using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Persimmon.VisualStudio.TestRunner.Internals
{
    internal sealed class PdbReader : IDisposable
    {
        private DiaSession diaSession_;

        public PdbReader()
        {
        }

        public void Dispose()
        {
            lock (this)
            {
                if (diaSession_ != null)
                {
                    diaSession_.Dispose();
                    diaSession_ = null;
                }
            }
        }

        public Task<bool> ReadAsync(string executablePath)
        {
            return Task.Factory.StartNew(() =>
            {
                lock (this)
                {
                    this.Dispose();

                    try
                    {
                        diaSession_ = new DiaSession(executablePath);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());
                        return false;
                    }

                    return true;
                }
            });
        }

        public DiaNavigationData GetNavigationData(
            string typeName,
            string methodName)
        {
            return (diaSession_ != null) ? diaSession_.GetNavigationData(typeName, methodName) : null;
        }
    }
}
