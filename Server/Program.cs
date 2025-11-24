using System;
using Core;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            var engineSettings = EngineSettings.Load();
            var project = new Project(Directory.GetCurrentDirectory());

            using (var scriptHost = new ScriptHost(project))
            using (var networkServer = new NetworkServer(7777, engineSettings, scriptHost))
            {
                if (!string.IsNullOrEmpty(project.Settings.MainMap))
                {
                    scriptHost.ExecuteCommand($"Game:LoadMap(\"{project.Settings.MainMap}\")");
                }

                scriptHost.Start();
                networkServer.Start();

                Console.WriteLine("Server is running. Press Enter to exit.");
                Console.ReadLine();
            }
        }
    }
}
