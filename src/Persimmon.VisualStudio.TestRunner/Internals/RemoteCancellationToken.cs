using System;
using System.Threading;

namespace Persimmon.VisualStudio.TestRunner.Internals
{
    public sealed class RemoteCancellationToken : MarshalByRefObject
    {
        private readonly CancellationToken token_;

        private RemoteCancellationToken(CancellationToken token)
        {
            token_ = token;
        }

        public void InternalRegisterSink(InternalRemoteCancellationTokenSink sink)
        {
            token_.Register(sink.Cancel);
        }

        public static CancellationToken AsToken(RemoteCancellationToken remoteToken)
        {
            var sink = new InternalRemoteCancellationTokenSink();
            remoteToken.InternalRegisterSink(sink);
            return sink.Token;
        }

        public static implicit operator CancellationToken(RemoteCancellationToken remoteToken)
        {
            return AsToken(remoteToken);
        }

        public static implicit operator RemoteCancellationToken(CancellationToken token)
        {
            return new RemoteCancellationToken(token);
        }

        public sealed class InternalRemoteCancellationTokenSink : MarshalByRefObject 
        {
            private readonly CancellationTokenSource cts_ = new CancellationTokenSource();

            internal InternalRemoteCancellationTokenSink()
            {
            }

            internal CancellationToken Token
            {
                get { return cts_.Token; }
            }

            public void Cancel()
            {
                cts_.Cancel();               
            }
        }
    }

    public static class RemoteCancellationTokenExtensions
    {
        public static CancellationToken AsToken(this RemoteCancellationToken token)
        {
            return RemoteCancellationToken.AsToken(token);
        }
    }
}
