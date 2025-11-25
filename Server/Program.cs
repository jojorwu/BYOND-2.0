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

            using (var scriptHost = new ScriptHost())
            using (var networkServer = new NetworkServer(IPAddress.Parse(settings.IpAddress), settings.Port))
            {
                scriptHost.Start();
                networkServer.Start();

                Console.WriteLine($"Server is running on {settings.IpAddress}:{settings.Port}. Press Enter to exit.");
                Console.ReadLine();
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
