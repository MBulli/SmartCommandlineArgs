using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmartCmdArgs.Helper
{
    static class DelayExecution
    {
        internal static void ExecuteAfter(TimeSpan delay, Action action)
        {
            ExecuteAfter(delay, CancellationToken.None, action);
        }

        internal static void ExecuteAfter(TimeSpan delay, CancellationToken cancelToken, Action action)
        {
            SynchronizationContext context = SynchronizationContext.Current;

            Task.Delay(delay, cancelToken).ContinueWith((_) =>
            {
                context.Post((__) => 
                {
                    if (!cancelToken.IsCancellationRequested)
                    {
                        action();
                    }                   
                }, null);
            }, cancelToken);
        }

    }
}
