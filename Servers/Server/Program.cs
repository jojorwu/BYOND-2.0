using Shared;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Core;
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
        services.AddSingleton<Shared.Config.IConfigurationManager>(sp => {
            var manager = new Shared.Config.ConfigurationManager();
            manager.RegisterFromAssemblies(typeof(Shared.Config.ConfigKeys).Assembly);
            manager.AddProvider(new Shared.Config.JsonConfigProvider("server_config.json"));
            manager.AddProvider(new Shared.Config.EnvironmentConfigProvider());
            manager.LoadAll();
            return manager;
        });

        services.Configure<ServerSettings>(configuration.GetSection("ServerSettings"));
        services.AddSingleton(resolver => {
            var manager = resolver.GetRequiredService<Shared.Config.IConfigurationManager>();
            return new ServerSettings(manager);
        });

        services.AddSingleton<IProject>(new Project(".")); // Assume server runs from project root

        services.AddSingleton<Shared.IJsonService, DMCompiler.Json.JsonService>();
        services.AddSingleton<Shared.ICompilerService, DMCompiler.CompilerService>();

        services.AddCoreServices();
        services.AddServerHostedServices();
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
