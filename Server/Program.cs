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
    private const string ConfigFileName = "server_config.json";
    static async Task Main(string[] args)
    {
        EnsureServerConfigFileExists();
        await CreateHostBuilder(args).Build().RunAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile(ConfigFileName, optional: false, reloadOnChange: true);
            })
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
        var settings = configuration.GetSection("ServerSettings").Get<ServerSettings>() ?? new ServerSettings();
        services.AddSingleton(settings);
        services.AddSingleton<IProject>(new Project(".")); // Assume server runs from project root

        // Core services
        services.AddSingleton<IGameState, GameState>();
        services.AddSingleton<IObjectTypeManager, ObjectTypeManager>();
        services.AddSingleton<IMapLoader, MapLoader>();
        services.AddSingleton<IMapApi, MapApi>();
        services.AddSingleton<IObjectApi, ObjectApi>();
        services.AddSingleton<IScriptApi, ScriptApi>();
        services.AddSingleton<IStandardLibraryApi, StandardLibraryApi>();
        services.AddSingleton<IGameApi, GameApi>();
        services.AddSingleton<IRegionApi, RegionApi>();
        services.AddSingleton<IDmmService, DmmService>();
        services.AddSingleton<ICompilerService, OpenDreamCompilerService>();
        services.AddSingleton<DreamVM>();
        services.AddSingleton<IDreamVM>(provider => provider.GetRequiredService<DreamVM>());
        services.AddSingleton<IDreamMakerLoader, DreamMakerLoader>();
        services.AddSingleton<IScriptManager>(provider =>
            new ScriptManager(
                provider.GetRequiredService<IProject>(),
                provider.GetServices<IScriptSystem>(),
                provider.GetRequiredService<IGameState>()
            )
        );
        services.AddSingleton<IPlayerManager>(provider =>
            new Core.PlayerManager(
                provider.GetRequiredService<IObjectApi>(),
                provider.GetRequiredService<IObjectTypeManager>(),
                provider.GetRequiredService<ServerSettings>()
            )
        );
        services.AddSingleton<IRegionManager>(provider =>
            new RegionManager(
                provider.GetRequiredService<IMap>(),
                provider.GetRequiredService<IScriptHost>(),
                provider.GetRequiredService<IGameState>(),
                provider.GetRequiredService<IPlayerManager>(),
                provider.GetRequiredService<ServerSettings>()
            )
        );
        services.AddSingleton<IScriptSystem, Core.Scripting.CSharp.CSharpSystem>();
        services.AddSingleton<IScriptSystem, Core.Scripting.LuaSystem.LuaSystem>();
        services.AddSingleton<IScriptSystem>(provider =>
            new Core.Scripting.DM.DmSystem(
                provider.GetRequiredService<IObjectTypeManager>(),
                provider.GetRequiredService<IDreamMakerLoader>(),
                provider.GetRequiredService<ICompilerService>(),
                provider.GetRequiredService<IDreamVM>(),
                () => provider.GetRequiredService<IScriptHost>()
            )
        );

        // Hosted services
        services.AddSingleton<ScriptHost>();
        services.AddSingleton<IScriptHost>(provider => provider.GetRequiredService<ScriptHost>());
        services.AddHostedService(provider => provider.GetRequiredService<ScriptHost>());

        services.AddSingleton<UdpServer>();
        services.AddSingleton<IUdpServer>(provider => provider.GetRequiredService<UdpServer>());
        services.AddHostedService(provider => provider.GetRequiredService<UdpServer>());

        services.AddSingleton<GameLoop>();
        services.AddHostedService(provider => provider.GetRequiredService<GameLoop>());

        services.AddSingleton<HttpServer>();
        services.AddHostedService<HttpServer>();
    }

    private static void EnsureServerConfigFileExists()
    {
        if (File.Exists(ConfigFileName))
            return;

        var serverSettings = new ServerSettings();
        var serverSettingsSection = new { ServerSettings = serverSettings };
        var json = JsonSerializer.Serialize(serverSettingsSection, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigFileName, json);
    }
}
