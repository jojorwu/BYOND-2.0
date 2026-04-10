using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Core;
using Shared.Services;
using Serilog;

namespace Editor;

/// <summary>
/// Entry point for the BYOND 2.0 Editor.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog((context, configuration) =>
            {
                configuration.ReadFrom.Configuration(context.Configuration);
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddCoreServices();
                services.AddEngineModule<EditorModule>();
            })
            .Build();

        await host.RunAsync();
    }
}
