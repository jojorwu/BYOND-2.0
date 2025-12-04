using Core;
using Core.VM.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Threading.Tasks;

namespace Server;

class Program
{
    static async Task Main(string[] args)
    {
        await CreateHostBuilder(args).Build().RunAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .UseSerilog((context, configuration) =>
            {
                configuration.ReadFrom.Configuration(context.Configuration);
            })
            .ConfigureServices((hostContext, services) =>
            {
                var serverSettings = new ServerSettings();
                hostContext.Configuration.GetSection("ServerSettings").Bind(serverSettings);
                services.AddSingleton(serverSettings);

                services.AddSingleton(new Project(".")); // Assume server runs from project root
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
                services.AddSingleton<ScriptHost>();
                services.AddSingleton<IScriptHost>(provider => provider.GetRequiredService<ScriptHost>());
                services.AddHostedService(provider => provider.GetRequiredService<ScriptHost>());

                services.AddSingleton<UdpServer>();
                services.AddHostedService(provider => provider.GetRequiredService<UdpServer>());

                services.AddSingleton<Game>();
                services.AddHostedService(provider => provider.GetRequiredService<Game>());
            });
}
