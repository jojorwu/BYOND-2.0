using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
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

            using (var scriptHost = new ScriptHost(project, gameState, settings))
            {
                scriptHost.Start();

                var ipAddress = settings.Network.Mode == NetworkMode.Automatic
                    ? IPAddress.Any // 0.0.0.0, listens on all available network interfaces
                    : IPAddress.Parse(settings.Network.IpAddress);

                var port = settings.Network.Port;

                using (var udpServer = new UdpServer(ipAddress, port, scriptHost, gameState))
                {
                    udpServer.Start();

                    var displayIp = settings.Network.Mode == NetworkMode.Automatic ? "0.0.0.0 (all interfaces)" : ipAddress.ToString();

                    Console.WriteLine($"Server '{settings.ServerName}' is running on {displayIp}:{port}.");
                    Console.WriteLine($"Description: {settings.ServerDescription}");
                    Console.WriteLine($"Max Players: {settings.MaxPlayers}");
                    Console.WriteLine("The process will run indefinitely.");

                    var tickInterval = 1000 / settings.Performance.TickRate;

                    while (true)
                    {
                        scriptHost.Tick();
                        Thread.Sleep(tickInterval);
                    }
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
