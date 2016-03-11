using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Persimmon.VisualStudio.TestRunner
{
    public interface ITestRunSink : ITestSink
    {
        void Progress(TestResult testResult);
    }
}
