using System;

namespace TestConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            System.Threading.Thread.Sleep(10000);
            Console.WriteLine("TestConsoleApp called with args: " + string.Join(",", args));
        }
    }
}
