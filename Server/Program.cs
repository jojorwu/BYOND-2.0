using System;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var scriptHost = new ScriptHost())
            {
                scriptHost.Start();

                Console.WriteLine("Server is running. Press Enter to exit.");
                Console.ReadLine();
            }
        }
    }
}
