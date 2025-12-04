using System;
using System.IO;
using System.Text.Json;
using Core;
using Core.VM.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Server;

class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);

        var serviceProvider = services.BuildServiceProvider();
        var game = serviceProvider.GetRequiredService<Game>();
        await game.Start();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var settings = LoadSettings();
        var project = new Project("."); // Assume server runs from project root

        services.AddSingleton(settings);
        services.AddSingleton(project);
        services.AddSingleton<GameState>();
        services.AddTransient<ObjectTypeManager>();
        services.AddTransient<DreamVM>();
        services.AddTransient<MapLoader>();
        services.AddTransient<IMapApi, MapApi>();
        services.AddTransient<IObjectApi, ObjectApi>();
        services.AddTransient<IScriptApi, ScriptApi>();
        services.AddTransient<IStandardLibraryApi, StandardLibraryApi>();
        services.AddTransient<IGameApi, GameApi>();
        services.AddTransient<ScriptManager>(provider =>
            new ScriptManager(
                provider.GetRequiredService<IGameApi>(),
                provider.GetRequiredService<ObjectTypeManager>(),
                provider.GetRequiredService<Project>(),
                provider.GetRequiredService<DreamVM>(),
                () => provider.GetRequiredService<IScriptHost>()
            )
        );
        services.AddSingleton<ScriptHost>(provider =>
            new ScriptHost(
                provider.GetRequiredService<Project>(),
                provider.GetRequiredService<ServerSettings>(),
                provider
            )
        );
        services.AddSingleton<IScriptHost>(provider => provider.GetRequiredService<ScriptHost>());
        services.AddSingleton<UdpServer>(provider =>
        {
            var settings = provider.GetRequiredService<ServerSettings>();
            var scriptHost = provider.GetRequiredService<IScriptHost>();
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
