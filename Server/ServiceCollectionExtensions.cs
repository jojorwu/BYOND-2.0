using Core;
using Core.VM.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared;

namespace Server
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddServerHostedServices(this IServiceCollection services)
        {
            services.AddSingleton(provider => new ScriptHost(
                provider.GetRequiredService<IProject>(),
                provider.GetRequiredService<ServerSettings>(),
                provider.GetRequiredService<IServiceProvider>(),
                provider.GetRequiredService<ILogger<ScriptHost>>(),
                provider.GetRequiredService<IGameState>()
            ));
            services.AddSingleton<IScriptHost>(provider => provider.GetRequiredService<ScriptHost>());
            services.AddHostedService(provider => provider.GetRequiredService<ScriptHost>());

            services.AddSingleton<INetworkService, NetworkService>();
            services.AddSingleton<NetworkEventHandler>();
            services.AddSingleton<UdpServer>();
            services.AddSingleton<IUdpServer>(provider => provider.GetRequiredService<UdpServer>());
            services.AddHostedService(provider => provider.GetRequiredService<UdpServer>());

            services.AddSingleton<IGameStateSnapshotter, GameStateSnapshotter>();
            services.AddSingleton(provider => new GlobalGameLoopStrategy(
                provider.GetRequiredService<IScriptHost>(),
                provider.GetRequiredService<IGameState>(),
                provider.GetRequiredService<IGameStateSnapshotter>(),
                provider.GetRequiredService<IUdpServer>()
            ));
            services.AddSingleton(provider =>
                new RegionalGameLoopStrategy(
                    provider.GetRequiredService<IScriptHost>(),
                    provider.GetRequiredService<IRegionManager>(),
                    provider.GetRequiredService<Core.Regions.IRegionActivationStrategy>(),
                    provider.GetRequiredService<IUdpServer>(),
                    provider.GetRequiredService<IGameState>(),
                    provider.GetRequiredService<IGameStateSnapshotter>(),
                    provider.GetRequiredService<ServerSettings>()
                )
            );
            services.AddSingleton<IGameLoopStrategy>(provider =>
            {
                var settings = provider.GetRequiredService<ServerSettings>();
                if (settings.Performance.EnableRegionalProcessing)
                {
                    return provider.GetRequiredService<RegionalGameLoopStrategy>();
                }
                else
                {
                    return provider.GetRequiredService<GlobalGameLoopStrategy>();
                }
            });

            services.AddSingleton<GameLoop>();
            services.AddHostedService(provider => provider.GetRequiredService<GameLoop>());

            services.AddSingleton<HttpServer>();
            services.AddHostedService<HttpServer>();

            return services;
        }
    }
}
