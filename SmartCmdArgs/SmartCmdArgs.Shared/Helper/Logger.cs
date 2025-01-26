using System;
using System.Diagnostics;
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

        public static void Error(string message, Exception ex = null)
        {
            ActivityLog.TryLogError(LogSource, AppendException(PrependThreadId(message), ex));
            WriteToDebugConsole("Error", message, ex);
        }

        [Conditional("DEBUG")]
        private static void WriteToDebugConsole(string type, string message, Exception ex = null)
        {
            Debug.WriteLine($"{LogSource}[{System.Threading.Thread.CurrentThread.ManagedThreadId}]|{type}: {AppendException(message, ex)}");
        }

        private static string PrependThreadId(string message)
        {
            return $"[{System.Threading.Thread.CurrentThread.ManagedThreadId}] {message}";
        }

        private static string AppendException(string message, Exception ex)
        {
            if (ex == null)
                return message;

            return message + Environment.NewLine + ex.ToString();
        }
    }
}
