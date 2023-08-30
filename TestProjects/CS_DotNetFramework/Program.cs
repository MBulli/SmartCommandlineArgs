using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS_DotNetFramework
{
    class Program
    {
        static void Main(string[] args)
        {
            // Display command line arguments
            Console.WriteLine("Command Line Arguments:");
            foreach (var arg in args)
            {
                Console.WriteLine(arg);
            }
        }
    }
}
