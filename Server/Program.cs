using System;
using System.IO;
using System.Text.Json;
using Core;
using Core.VM.Runtime;
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
        services.AddSingleton<GameState>();
        services.AddScoped<ObjectTypeManager>();
        services.AddScoped<DreamVM>();
        services.AddScoped<MapLoader>();
        services.AddScoped<IMapApi, MapApi>();
        services.AddScoped<IObjectApi, ObjectApi>();
        services.AddScoped<IScriptApi, ScriptApi>();
        services.AddScoped<IStandardLibraryApi, StandardLibraryApi>();
        services.AddScoped<IGameApi, GameApi>();
        services.AddScoped<ScriptManager>(provider =>
            new ScriptManager(
                provider.GetRequiredService<IGameApi>(),
                provider.GetRequiredService<ObjectTypeManager>(),
                provider.GetRequiredService<Project>(),
                provider.GetRequiredService<DreamVM>(),
                () => provider.GetRequiredService<IScriptHost>()
            )
        );
        services.AddSingleton<ScriptHost>(provider =>
        {
            var project = provider.GetRequiredService<Project>();
            var gameState = provider.GetRequiredService<GameState>();
            var settings = provider.GetRequiredService<ServerSettings>();
            return new ScriptHost(project, gameState, settings, provider);
        });
        services.AddSingleton<IScriptHost>(provider => provider.GetRequiredService<ScriptHost>());
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
