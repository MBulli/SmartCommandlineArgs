using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.Tests.Utils
{
    public static class Config
    {
#if VS17
        public const string Version = "2022";
#elif VS16
        public const string Version = "2019";
#elif VS15
        public const string Version = "2017";
#endif
    }
}
