using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Threading;
using Task = System.Threading.Tasks.Task;

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
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await Task.Delay(delay, cancelToken);

                if (!cancelToken.IsCancellationRequested)
                {
                    action();
                }
            }).Task.Forget();
        }
    }
}
