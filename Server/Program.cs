using System;
using Core;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            var engineSettings = EngineSettings.Load();

            using (var scriptHost = new ScriptHost())
            using (var networkServer = new NetworkServer(7777, engineSettings))
            {
                scriptHost.Start();
                networkServer.Start();

                Console.WriteLine("Server is running. Press Enter to exit.");
                Console.ReadLine();
            }
        }
    }
}
