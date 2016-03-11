using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Persimmon.VisualStudio.TestRunner;
using Persimmon.VisualStudio.TestExplorer.Sinks;

namespace Persimmon.VisualStudio.TestExplorer
{
    [FileExtension(".dll")]
    [ExtensionUri(Constant.ExtensionUriString)]
    [DefaultExecutorUri(Constant.ExtensionUriString)]
    public sealed class TestAdapter : ITestDiscoverer, ITestExecutor
    {
        private static readonly object lock_ = new object();
        private static bool ready_;

        private readonly Version version_ = typeof(TestAdapter).Assembly.GetName().Version;
        private readonly ConcurrentQueue<CancellationTokenSource> cancellationTokens_ =
            new ConcurrentQueue<CancellationTokenSource>();

        [Conditional("DEBUG")]
        private void WaitingForAttachDebugger()
        {
            lock (lock_)
            {
                if (ready_ == false)
                {
                    NativeMethods.MessageBox(
                        IntPtr.Zero,
                        string.Format("Waiting for DEBUG ({0}) ...", Process.GetCurrentProcess().Id),
                        this.GetType().FullName,
                        NativeMethods.MessageBoxOptions.IconWarning | NativeMethods.MessageBoxOptions.OkOnly);
                    ready_ = true;
                }
            }
        }

        public void DiscoverTests(
            IEnumerable<string> sources,
            IDiscoveryContext discoveryContext,
            IMessageLogger logger,
            ITestCaseDiscoverySink discoverySink)
        {
            this.WaitingForAttachDebugger();

            logger.SendMessage(
                TestMessageLevel.Informational,
                string.Format("Persimmon Test Explorer {0} discovering tests started", version_));
            try
            {
                var testExecutor = new TestExecutor();
                var sink = new TestDiscoverySink(discoveryContext, logger, discoverySink);

                // Discover must synch execute.
                var task = Task.WhenAll(sources.Select(
                    targetAssemblyPath => testExecutor.DiscoverAsync(targetAssemblyPath, sink)));

                task.Wait();
            }
            catch (Exception ex)
            {
                logger.SendMessage(
                    TestMessageLevel.Error,
                    ex.ToString());
            }
            finally
            {
                logger.SendMessage(
                    TestMessageLevel.Informational,
                    string.Format("Persimmon Test Explorer {0} discovering tests finished", version_));
            }
        }

        public void RunTests(
            IEnumerable<string> sources,
            IRunContext runContext,
            IFrameworkHandle frameworkHandle)
        {
            this.WaitingForAttachDebugger();

            frameworkHandle.SendMessage(
                TestMessageLevel.Informational,
                string.Format("Persimmon Test Explorer {0} run tests started", version_));
            try
            {
                var testExecutor = new TestExecutor();
                var sink = new TestRunSink(runContext, frameworkHandle);

                // Register cancellation token.
                var cts = new CancellationTokenSource();
                cancellationTokens_.Enqueue(cts);

                // Start tests.
                // TODO: Generate TestCase here...
                var task = Task.WhenAll(sources.Select(targetAssemblyPath =>
                    testExecutor.RunAsync(targetAssemblyPath, new TestCase[0], sink, cts.Token)));

                task.Wait();
            }
            catch (Exception ex)
            {
                frameworkHandle.SendMessage(
                    TestMessageLevel.Error,
                    ex.ToString());
            }
            finally
            {
                frameworkHandle.SendMessage(
                    TestMessageLevel.Informational,
                    string.Format("Persimmon Test Explorer {0} run tests finished", version_));
            }
        }

        public void RunTests(
            IEnumerable<TestCase> tests,
            IRunContext runContext,
            IFrameworkHandle frameworkHandle)
        {
            this.WaitingForAttachDebugger();

            frameworkHandle.SendMessage(
                TestMessageLevel.Informational,
                string.Format("Persimmon Test Explorer {0} run tests started", version_));
            try
            {
                var testExecutor = new TestExecutor();
                var sink = new TestRunSink(runContext, frameworkHandle);

                // Register cancellation token.
                var cts = new CancellationTokenSource();
                cancellationTokens_.Enqueue(cts);

                // Start tests.
                var task = Task.WhenAll(tests.GroupBy(testCase => testCase.Source).
                    Select(g => testExecutor.RunAsync(g.Key, g.ToArray(), sink, cts.Token)));

                task.Wait();
            }
            catch (Exception ex)
            {
                frameworkHandle.SendMessage(
                    TestMessageLevel.Error,
                    ex.ToString());
            }
            finally
            {
                frameworkHandle.SendMessage(
                    TestMessageLevel.Informational,
                    string.Format("Persimmon Test Explorer {0} run tests finished", version_));
            }
        }

        public void Cancel()
        {
            // Cancel all tasks.
            foreach (var cts in cancellationTokens_)
            {
                cts.Cancel();
            }
        }

#if false
        interface ITestDiscoverer with
        member this.DiscoverTests(sources: string seq, context: IDiscoveryContext,logger: IMessageLogger, sink: ITestCaseDiscoverySink) =
            try
            logger.SendMessage(TestMessageLevel.Informational, sprintf "Persimmon Test Explorer %s discovering tests is started" version)
            try
                runner.DiscoverTests(sources, sink)
            with e ->
                logger.SendMessage(TestMessageLevel.Error, e.ToString())
            finally
            logger.SendMessage(TestMessageLevel.Informational, sprintf "Persimmon Test Explorer %s discovering tests is finished" version)
        interface ITestExecutor with
        member this.RunTests(tests: TestCase seq, runContext: IRunContext, handle: IFrameworkHandle) =
            (this :> ITestExecutor).RunTests(tests |> Seq.map (fun c -> c.Source), runContext, handle)
        member this.RunTests(sources: string seq, _: IRunContext, handle: IFrameworkHandle) =
            runner.RunTests(sources, handle)
        member __.Cancel() =
            ()
#endif
    }
}
