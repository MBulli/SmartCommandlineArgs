using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project1
{
    class Program
    {
        static void Main(string[] args)
        {
            var argString = string.Join(" ", args);
            File.WriteAllText("../../../CmdLineArgs.txt", argString);
        }
    }
}
