using System;

namespace CS_DotNetCore
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

            // Display environment variables
            Console.WriteLine("\nEnvironment Variables:");
            foreach (var key in Environment.GetEnvironmentVariables().Keys)
            {
                Console.WriteLine($"{key} = {Environment.GetEnvironmentVariable(key.ToString())}");
            }
        }
    }
}
