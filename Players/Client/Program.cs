using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var launchOptions = ParseArguments(args);

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton(launchOptions);
                    services.AddClientServices();
                })
                .Build();

            await host.StartAsync();

            var game = host.Services.GetRequiredService<Game>();
            game.Run();

            await host.StopAsync();
        }

        private static ClientLaunchOptions ParseArguments(string[] args)
        {
            var options = new ClientLaunchOptions();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--connect" && i + 1 < args.Length)
                {
                    options.AutoConnectAddress = args[i + 1];
                    i++;
                }
            }
            return options;
        }
    }

    public class ClientLaunchOptions
    {
        public string? AutoConnectAddress { get; set; }
    }
}
