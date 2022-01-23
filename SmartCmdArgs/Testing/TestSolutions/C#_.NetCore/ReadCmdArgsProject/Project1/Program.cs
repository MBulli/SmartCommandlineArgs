using System;
using System.IO;

namespace Project1
{
    class Program
    {
        static void Main(string[] args)
        {
            var argString = string.Join(" ", args);
            File.WriteAllText("../CmdLineArgs.txt", argString);
        }
    }
}