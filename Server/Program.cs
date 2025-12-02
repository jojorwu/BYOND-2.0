using System;
using System.IO;
using System.Text.Json;
using Core;
using Microsoft.Extensions.DependencyInjection;
using Server;

class Program
{
    static void Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);

        var serviceProvider = services.BuildServiceProvider();
        var game = serviceProvider.GetRequiredService<Game>();
        game.Start();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var settings = LoadSettings();
        var project = new Project("."); // Assume server runs from project root

        services.AddSingleton(settings);
        services.AddSingleton(project);

        services.AddCoreServices();

        services.AddSingleton<ScriptHost>();
        services.AddSingleton<UdpServer>(provider =>
        {
            var settings = provider.GetRequiredService<ServerSettings>();
            var scriptHost = provider.GetRequiredService<ScriptHost>();
            var gameState = provider.GetRequiredService<GameState>();
            return new UdpServer(System.Net.IPAddress.Any, settings.Network.UdpPort, scriptHost, gameState, settings);
        });
        services.AddSingleton<Game>();
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
