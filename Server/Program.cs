using System;
using System;
using System.IO;
using System.Net;
using System.Text.Json;
using Core;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            var settings = LoadSettings();
            var project = new Project("."); // Assume server runs from project root
            var gameState = new GameState();

            using (var scriptHost = new ScriptHost(project, gameState))
            {
                scriptHost.Start();

                using (var udpServer = new UdpServer(IPAddress.Parse(settings.IpAddress), settings.Port, scriptHost, gameState))
                {
                    udpServer.Start();

                    Console.WriteLine($"Server is running on {settings.IpAddress}:{settings.Port}. The process will run indefinitely.");
                    new System.Threading.ManualResetEvent(false).WaitOne();
                }
            }
        }

        private static ServerSettings LoadSettings()
        {
            var configPath = "server_config.json";
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                return JsonSerializer.Deserialize<ServerSettings>(json) ?? new ServerSettings();
            }
            else
            {
                var settings = new ServerSettings();
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
                return settings;
            }
        }
    }
}
