using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Persimmon.VisualStudio.TestRunner
{
    public interface ITestDiscoverSink : ITestSink
    {
        void Progress(TestCase testCase);
    }
}
