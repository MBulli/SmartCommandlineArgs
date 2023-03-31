using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SmartCmdArgs.Helper
{
    public static class PathHelper
    {
        public static string MakePathAbsolute(string path, string baseDir)
        {
            try
            {
                var drive = Path.GetPathRoot(path);

                if (!Path.IsPathRooted(path))
                {
                    if (baseDir == null)
                        return null;

                    path = Path.Combine(baseDir, path);
                }
                else if (drive == "\\")
                {
                    if (baseDir == null)
                        return null;

                    var baseDrive = Path.GetPathRoot(baseDir);
                    path = Path.Combine(baseDrive, path.Substring(1));
                }

                return Path.GetFullPath(path);
            }
            catch (ArgumentException)
            {
                // gets thrown when there are illegal characters in the path
                return null;
            }
            catch (NotSupportedException)
            {
                // gets thrown if there is a colon on any position other than after the drive letter
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
