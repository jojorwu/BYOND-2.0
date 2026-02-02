using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddClientServices();
                })
                .Build();

            await host.StartAsync();

            var game = host.Services.GetRequiredService<Game>();
            game.Run();

            await host.StopAsync();
        }
    }
}
