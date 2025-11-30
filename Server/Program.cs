using System;
using System.IO;
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

            var game = new Game(project, settings);
            game.Start();
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
