using System;
using System.Collections.Generic;
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
            ActivityLog.TryLogInformation(LogSource, message);
        }

        public static void Warn(string message)
        {
            ActivityLog.TryLogWarning(LogSource, message);
        }

        public static void Error(string message)
        {
            ActivityLog.TryLogError(LogSource, message);
        }
    }
}
