using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.Helper
{
    static class Extensions
    {
        public static T GetValueOrDefault<K, T>(this IDictionary<K, T> dict, K key, T defaultValue = default(T))
        {
            T item;
            return dict.TryGetValue(key, out item) ? item : defaultValue;
        }

        public static FileSystemWatcherDisabledContext TemporarilyDisable(this System.IO.FileSystemWatcher watcher)
        {
            return new FileSystemWatcherDisabledContext(watcher);
        }
    }
}
