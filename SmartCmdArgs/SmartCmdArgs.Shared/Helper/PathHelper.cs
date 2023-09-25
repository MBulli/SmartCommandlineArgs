using System;
using System.IO;

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

        public static string MakeRelativePath(string targetPath, string basePath)
        {
            if (string.IsNullOrEmpty(targetPath)) throw new ArgumentNullException("targetPath");
            if (string.IsNullOrEmpty(basePath)) throw new ArgumentNullException("basePath");

            // Make sure the paths end with a directory separator
            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                basePath += Path.DirectorySeparatorChar;

            if (!targetPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                targetPath += Path.DirectorySeparatorChar;

            var baseUri = new Uri(basePath);
            var targetUri = new Uri(targetPath);
            var relativeUri = baseUri.MakeRelativeUri(targetUri);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            return relativePath.Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
