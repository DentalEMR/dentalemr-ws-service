using System;
using System.IO;

namespace TestConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            System.Threading.Thread.Sleep(10000);
            Console.WriteLine($"TestConsoleApp called, running in working directory {Directory.GetCurrentDirectory()}, with args: {string.Join(",", args)}");
        }
    }
}
