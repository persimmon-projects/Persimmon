using System;
using System.Threading.Tasks;

namespace Persimmon.VisualStudio.TestRunner.Internals
{
    public sealed class RemoteTask<T> : MarshalByRefObject
    {
        private readonly Task<T> task_;

        private RemoteTask(Task<T> task)
        {
            task_ = task;
        }

        public void InternalRegisterSink(InternalRemoteTaskSink sink)
        {
            task_.ContinueWith(_ =>
            {
                if (task_.IsFaulted) sink.SetException(task_.Exception);
                else if (task_.IsCanceled) sink.SetCanceled();
                else if (task_.IsCompleted) sink.SetResult(task_.Result);
            });
        }

        public static Task<T> AsTask(RemoteTask<T> remoteTask)
        {
            var sink = new InternalRemoteTaskSink();
            remoteTask.InternalRegisterSink(sink);
            return sink.Task;
        }
        
        public static implicit operator RemoteTask<T>(Task<T> task)
        {
            return new RemoteTask<T>(task);
        }

        public static implicit operator Task<T>(RemoteTask<T> remoteTask)
        {
            return AsTask(remoteTask);
        }

        public sealed class InternalRemoteTaskSink : MarshalByRefObject
        {
            private readonly TaskCompletionSource<T> tcs_ = new TaskCompletionSource<T>();

            internal InternalRemoteTaskSink()
            {
            }

            internal Task<T> Task
            {
                get { return tcs_.Task; }
            }

            public void SetResult(T value)
            {
                tcs_.SetResult(value);
            }

            public void SetException(Exception ex)
            {
                tcs_.SetException(ex);
            }

            public void SetCanceled()
            {
                tcs_.SetCanceled();
            }
        }
    }

    public sealed class RemoteTask : MarshalByRefObject
    {
        private readonly Task task_;

        private RemoteTask(Task task)
        {
            task_ = task;
        }

        public void InternalRegisterSink(InternalRemoteTaskSink sink)
        {
            task_.ContinueWith(_ =>
            {
                if (task_.IsFaulted) sink.SetException(task_.Exception);
                else if (task_.IsCanceled) sink.SetCanceled();
                else if (task_.IsCompleted) sink.SetCompleted();
            });
        }

        public static Task AsTask(RemoteTask remoteTask)
        {
            var sink = new InternalRemoteTaskSink();
            remoteTask.InternalRegisterSink(sink);
            return sink.Task;
        }

        public static implicit operator RemoteTask(Task task)
        {
            return new RemoteTask(task);
        }

        public static implicit operator Task(RemoteTask remoteTask)
        {
            return AsTask(remoteTask);
        }

        public sealed class InternalRemoteTaskSink : MarshalByRefObject
        {
            private readonly TaskCompletionSource<int> tcs_ = new TaskCompletionSource<int>();

            internal InternalRemoteTaskSink()
            {
            }

            internal Task Task
            {
                get { return tcs_.Task; }
            }

            public void SetCompleted()
            {
                tcs_.SetResult(0);
            }

            public void SetException(Exception ex)
            {
                tcs_.SetException(ex);
            }

            public void SetCanceled()
            {
                tcs_.SetCanceled();
            }
        }
    }

    public static class RemoteTaskExtensions
    {
        public static Task<T> AsTask<T>(this RemoteTask<T> remoteTask)
        {
            return RemoteTask<T>.AsTask(remoteTask);
        }

        public static Task AsTask(this RemoteTask remoteTask)
        {
            return RemoteTask.AsTask(remoteTask);
        }
    }
}
