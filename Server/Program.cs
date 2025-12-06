using Shared;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Core;
using Core.VM.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Server;

class Program
{
    static async Task Main(string[] args)
    {
        await CreateHostBuilder(args).Build().RunAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog((context, configuration) =>
            {
                configuration.ReadFrom.Configuration(context.Configuration);
            })
            .ConfigureServices((hostContext, services) =>
            {
                var settings = LoadSettings(hostContext.Configuration);

                services.AddSingleton(settings);
                services.AddSingleton<IProject>(new Project(".")); // Assume server runs from project root
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
                        provider.GetRequiredService<IProject>(),
                        provider.GetRequiredService<DreamVM>(),
                        () => provider.GetRequiredService<IScriptHost>()
                    )
                );
                services.AddSingleton<ScriptHost>();
                services.AddSingleton<IScriptHost>(provider => provider.GetRequiredService<ScriptHost>());
                services.AddHostedService(provider => provider.GetRequiredService<ScriptHost>());

                services.AddSingleton<UdpServer>();
                services.AddHostedService(provider => provider.GetRequiredService<UdpServer>());

                services.AddSingleton<Game>();
                services.AddHostedService(provider => provider.GetRequiredService<Game>());
            });

    private static ServerSettings LoadSettings(IConfiguration configuration)
    {
        var settings = new ServerSettings();
        configuration.GetSection("ServerSettings").Bind(settings);

        var configPath = "server_config.json";
        if (!File.Exists(configPath))
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }

        return settings;
    }
}
