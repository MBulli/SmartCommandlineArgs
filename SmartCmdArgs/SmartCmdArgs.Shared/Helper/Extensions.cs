using System;
using System.Collections.Generic;
using System.Linq;

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

        public static bool Contains(this string str, string value, StringComparison comparisonType)
        {
            return str.IndexOf(value, comparisonType) >= 0;
        }

        public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            foreach (var item in enumerable)
            {
                action(item);
            }
        }

        public static void AddRange<T>(this HashSet<T> that, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                that.Add(item);
            }
        }

        public static bool HasMultipleItems<T>(this IEnumerable<T> enumerable)
        {
            return enumerable.Skip(1).Any();
        }
    }
}
