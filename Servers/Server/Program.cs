using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
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
        var settings = configuration.GetSection("ServerSettings").Get<ServerSettings>() ?? new ServerSettings();
        services.AddSingleton(settings);
        services.AddSingleton<IProject>(new Project(".")); // Assume server runs from project root

        services.AddSingleton<Shared.Interfaces.IJsonService, DMCompiler.Json.JsonService>();
        services.AddSingleton<Shared.Interfaces.ICompilerService, DMCompiler.CompilerService>();

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
