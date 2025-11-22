using System;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var scriptHost = new ScriptHost())
            using (var networkServer = new NetworkServer(7777))
            {
                scriptHost.Start();
                networkServer.Start();

                Console.WriteLine("Server is running. Press Enter to exit.");
                Console.ReadLine();
            }
        }
    }
}
