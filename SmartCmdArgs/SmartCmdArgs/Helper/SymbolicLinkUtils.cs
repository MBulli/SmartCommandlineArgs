using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

// copied from http://troyparsons.com/blog/2012/03/symbolic-links-in-c-sharp/

namespace SmartCmdArgs.Helper
{
    static class SymbolicLinkUtils
    {
        [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetFinalPathNameByHandle([In] IntPtr hFile, [Out] StringBuilder lpszFilePath, [In] int cchFilePath, [In] int dwFlags);

        public static string GetRealPath(string path)
        {
            string realPath = path;
            FileStream stream = null;
            try
            {
                try
                {
                    stream = File.Open(path, FileMode.Open);
                }
                catch (FileNotFoundException)
                {
                    stream = File.Create(path, 512, FileOptions.DeleteOnClose);
                }
                var handle = stream.SafeFileHandle;

                if (handle == null)
                    return realPath;

                StringBuilder result = new StringBuilder(512);
                int mResult = GetFinalPathNameByHandle(handle.DangerousGetHandle(), result, result.Capacity, 0);
                if (mResult < 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                if (result.Length >= 4 && result[0] == '\\' && result[1] == '\\' && result[2] == '?' && result[3] == '\\')
                    realPath = result.ToString().Substring(4);      // remove "\\?\"
                else
                    realPath = result.ToString();
            }
            catch (Exception)
            {
                // TODO: logging
                Debug.WriteLine($"Could not resolve symbolic link '{path}'");
            }
            finally
            {
                stream?.Close();
            }
            return realPath;
        }
    }
}
