using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Microsoft.Extensions.DependencyInjection;
using Shared.Services;

namespace Launcher
{
    class Program
    {
        static void Main(string[] args)
        {
            var services = new ServiceCollection();

            services.AddSharedEngineServices();
            services.AddSharedMessagingServices();
            services.AddSingleton<Launcher>();

            var serviceProvider = services.BuildServiceProvider();

            var launcher = serviceProvider.GetRequiredService<Launcher>();
            launcher.Run();
        }
    }
}
