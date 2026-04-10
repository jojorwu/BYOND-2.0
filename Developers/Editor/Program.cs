using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Core;
using Shared.Services;
using Shared.Config;
using Serilog;

namespace Editor
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
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
}
