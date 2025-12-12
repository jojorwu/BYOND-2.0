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
        services.AddSingleton<Ss14MapLoader>(provider => new Ss14MapLoader(provider.GetRequiredService<IObjectTypeManager>()));
        services.AddSingleton<IGameState, GameState>();
        services.AddSingleton<IRestartService, RestartService>();
        services.AddSingleton<IObjectTypeManager, ObjectTypeManager>();
        services.AddSingleton<IMapLoader>(provider =>
            new MapLoader(
                provider.GetRequiredService<IObjectTypeManager>(),
                provider.GetRequiredService<Ss14MapLoader>()
            )
        );
        services.AddSingleton<IMapApi, MapApi>();
        services.AddSingleton<IObjectApi>(provider => new ObjectApi(
            provider.GetRequiredService<IGameState>(),
            provider.GetRequiredService<IObjectTypeManager>(),
            provider.GetRequiredService<IMapApi>(),
            provider.GetRequiredService<ServerSettings>()
        ));
        services.AddSingleton<IScriptApi, ScriptApi>();
        services.AddSingleton<IStandardLibraryApi, StandardLibraryApi>();
        services.AddSingleton<IGameApi, GameApi>();
        services.AddSingleton<IDmmService, DmmService>();
        services.AddSingleton<ICompilerService, OpenDreamCompilerService>();
        services.AddSingleton<DreamVM>();
        services.AddSingleton<IDreamVM>(provider => provider.GetRequiredService<DreamVM>());
        services.AddSingleton<IDreamMakerLoader, DreamMakerLoader>();
        services.AddSingleton<IScriptManager, ScriptManager>();
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
