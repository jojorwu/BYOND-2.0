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
            services.AddEngineModule<LauncherModule>();

            var serviceProvider = services.BuildServiceProvider();

            var launcher = serviceProvider.GetRequiredService<Launcher>();
            launcher.Run();
        }
    }
}
