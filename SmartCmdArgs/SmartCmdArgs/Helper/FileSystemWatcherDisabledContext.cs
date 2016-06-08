using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.Helper
{
    class FileSystemWatcherDisabledContext : IDisposable
    {
        private FileSystemWatcher watcher;

        public FileSystemWatcherDisabledContext(FileSystemWatcher watcher)
        {
            this.watcher = watcher;

            if (this.watcher != null)
            {
                this.watcher.EnableRaisingEvents = false;
            }
        }

        public void Dispose()
        {
            if (watcher != null)
            {
                this.watcher.EnableRaisingEvents = true;
            }
        }
    }
}
