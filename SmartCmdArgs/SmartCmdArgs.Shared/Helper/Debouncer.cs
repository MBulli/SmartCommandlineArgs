using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

using Task = System.Threading.Tasks.Task;

namespace SmartCmdArgs.Helper
{
    public class Debouncer
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly TimeSpan _debounceTime;

        public Debouncer(TimeSpan debounceTime)
        {
            _debounceTime = debounceTime;
        }

        public void Debounce(Action action)
        {
            var isOnUiThread = ThreadHelper.CheckAccess();

            Task.Run(async () => {
                if (await _semaphore.WaitAsync(0))
                {
                    try
                    {
                        await Task.Delay(_debounceTime);

                        if (isOnUiThread)
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    }
                    finally
                    {
                        _semaphore.Release();
                    }

                    // the action gets called after _semaphore.Release() to make sure that
                    // if the debounce is called while the action is processed
                    // a new call gets enqueued to prevent loss of data
                    action();
                }
            }).Forget();
        }
    }

    public class DebouncerTable<TKey>
        where TKey : class
    {
        private readonly SemaphoreSlim _nullSemaphore =  new SemaphoreSlim(1, 1);
        private readonly ConditionalWeakTable<TKey, SemaphoreSlim> _semaphores = new ConditionalWeakTable<TKey, SemaphoreSlim>();
        private readonly TimeSpan _debounceTime;

        public DebouncerTable(TimeSpan debounceTime)
        {
            _debounceTime = debounceTime;
        }

        public void Debounce(TKey key, Action action)
        {
            var isOnUiThread = ThreadHelper.CheckAccess();

            Task.Run(async () => {
                var semaphore = key == null
                    ? _nullSemaphore
                    : _semaphores.GetValue(key, (_) => new SemaphoreSlim(1, 1));

                if (await semaphore.WaitAsync(0))
                {
                    try
                    {
                        await Task.Delay(_debounceTime);

                        if (isOnUiThread)
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    }
                    finally
                    {
                        semaphore.Release();
                    }

                    // the action gets called after _semaphore.Release() to make sure that
                    // if the debounce is called while the action is processed
                    // a new call gets enqueued to prevent loss of data
                    action();
                }
            }).Forget();
        }
    }
}

