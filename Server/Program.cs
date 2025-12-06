using Shared;
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
                ConfigureServices(services, hostContext.Configuration);
            });

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var settings = LoadSettings(configuration);
        services.AddSingleton(settings);
        services.AddSingleton<IProject>(new Project(".")); // Assume server runs from project root

        // Core services
        services.AddSingleton<GameState>();
        services.AddSingleton<IObjectTypeManager, ObjectTypeManager>();
        services.AddSingleton<MapLoader>();
        services.AddSingleton<IMapApi, MapApi>();
        services.AddSingleton<IObjectApi, ObjectApi>();
        services.AddSingleton<IScriptApi, ScriptApi>();
        services.AddSingleton<IStandardLibraryApi, StandardLibraryApi>();
        services.AddSingleton<IGameApi, GameApi>();
        services.AddSingleton<IDmmService, DmmService>();
        services.AddSingleton<ICompilerService, OpenDreamCompilerService>();
        services.AddSingleton<DreamVM>();
        services.AddSingleton<ScriptManager>();

        // Hosted services
        services.AddSingleton<ScriptHost>();
        services.AddSingleton<IScriptHost>(provider => provider.GetRequiredService<ScriptHost>());
        services.AddHostedService(provider => provider.GetRequiredService<ScriptHost>());

        services.AddSingleton<UdpServer>();
        services.AddHostedService(provider => provider.GetRequiredService<UdpServer>());

        services.AddSingleton<Game>();
        services.AddHostedService(provider => provider.GetRequiredService<Game>());
    }


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
