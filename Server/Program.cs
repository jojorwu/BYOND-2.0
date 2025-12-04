using System;
using System.IO;
using System.Text.Json;
using Core;
using Core.VM.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Server;

class Program
{
    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            var host = CreateHostBuilder(args).Build();
            var game = host.Services.GetRequiredService<Game>();
            await game.Start();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((hostContext, services) =>
            {
                ConfigureServices(services);
            });

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
                provider,
                provider.GetRequiredService<ILogger<ScriptHost>>(),
                provider.GetRequiredService<ILogger<ScriptingEnvironment>>()
            )
        );
        services.AddSingleton<IScriptHost>(provider => provider.GetRequiredService<ScriptHost>());
        services.AddSingleton<UdpServer>(provider =>
        {
            var settings = provider.GetRequiredService<ServerSettings>();
            var scriptHost = provider.GetRequiredService<IScriptHost>();
            var gameState = provider.GetRequiredService<GameState>();
            var logger = provider.GetRequiredService<ILogger<UdpServer>>();
            return new UdpServer(System.Net.IPAddress.Any, settings.Network.UdpPort, scriptHost, gameState, settings, logger);
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
