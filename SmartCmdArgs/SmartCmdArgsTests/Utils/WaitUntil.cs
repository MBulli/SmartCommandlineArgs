using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SmartCmdArgsTests.Utils
{
    class WaitUntil : IDisposable
    {
        private bool wait = true;
        private int resolutions;

        public WaitUntil(int resolution = 100)
        {
            this.resolutions = resolution;
        }

        public void Finish()
        {
            wait = false;
        }
        
        public void Dispose()
        {
            while (wait)
            {
                Thread.Sleep(resolutions);
            }
        }
    }
}
