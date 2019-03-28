using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace SmartCmdArgs.Helper
{
    static class Logger
    {
        private static string LogSource = "SmartCommandlineArgs";

        public static void Info(string message)
        {
            ActivityLog.TryLogInformation(LogSource, PrependThreadId(message));
            WriteToDebugConsole("Info", message);
        }

        public static void Warn(string message)
        {
            ActivityLog.TryLogWarning(LogSource, PrependThreadId(message));
            WriteToDebugConsole("Warning", message);
        }

        public static void Error(string message)
        {
            ActivityLog.TryLogError(LogSource, PrependThreadId(message));
            WriteToDebugConsole("Error", message);
        }

        [Conditional("DEBUG")]
        private static void WriteToDebugConsole(string type, string message)
        {
            Debug.WriteLine($"{LogSource}[{System.Threading.Thread.CurrentThread.ManagedThreadId}]|{type}: {message}");
        }

        private static string PrependThreadId(string message)
        {
            return $"[{System.Threading.Thread.CurrentThread.ManagedThreadId}] {message}";
        }
    }
}
