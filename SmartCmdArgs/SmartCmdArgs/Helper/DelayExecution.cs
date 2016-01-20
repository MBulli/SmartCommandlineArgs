using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SmartCmdArgs.Helper
{
    static class DelayExecution
    {
        internal static void ExecuteAfter(TimeSpan delay, Action action)
        {
            System.Threading.Timer timer = null;
            SynchronizationContext context = SynchronizationContext.Current;

            timer = new System.Threading.Timer(
                (ignore) =>
                {
                    timer.Dispose();

                    context.Post(ignore2 => action(), null);
                }, null, delay, TimeSpan.FromMilliseconds(-1));
        }
    }
}
