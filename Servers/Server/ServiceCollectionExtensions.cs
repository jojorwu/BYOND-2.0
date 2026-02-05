using Core;
using Core.VM.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Shared;
using Shared.Interfaces;
using System;

namespace Server
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all server-specific hosted services, including networking, game loop, and the main application coordinator.
        /// </summary>
        public static IServiceCollection AddServerHostedServices(this IServiceCollection services)
        {
            return services
                .AddServerDiagnosticServices()
                .AddServerCoreServices()
                .AddServerNetworkingServices()
                .AddServerGameLoopServices()
                .AddServerApplicationServices();
        }

        private static IServiceCollection AddServerDiagnosticServices(this IServiceCollection services)
        {
            services.AddSingleton<PerformanceMonitor>();
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<PerformanceMonitor>());
            return services;
        }

        private static IServiceCollection AddServerCoreServices(this IServiceCollection services)
        {
            services.AddSingleton<IServerContext, ServerContext>();

            services.AddSingleton<IScriptWatcher, ScriptWatcher>();
            services.AddSingleton(provider => new ScriptHost(
                provider.GetRequiredService<IScriptWatcher>(),
                provider.GetRequiredService<ServerSettings>(),
                provider.GetRequiredService<IServiceProvider>(),
                provider.GetRequiredService<ILogger<ScriptHost>>(),
                provider.GetRequiredService<IGameState>()
            ));
            services.AddSingleton<IScriptHost>(provider => provider.GetRequiredService<ScriptHost>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<ScriptHost>());

            return services;
        }

        private static IServiceCollection AddServerNetworkingServices(this IServiceCollection services)
        {
            services.AddSingleton<NetDataWriterPool>();
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<NetDataWriterPool>());

            services.AddSingleton<NetworkService>();
            services.AddSingleton<INetworkService>(p => p.GetRequiredService<NetworkService>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<NetworkService>());

            services.AddSingleton<NetworkEventHandler>();

            services.AddSingleton<UdpServer>();
            services.AddSingleton<IUdpServer>(provider => provider.GetRequiredService<UdpServer>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<UdpServer>());

            return services;
        }

        private static IServiceCollection AddServerGameLoopServices(this IServiceCollection services)
        {
            services.AddSingleton<GameStateSnapshotter>();
            services.AddSingleton<IGameStateSnapshotter>(p => p.GetRequiredService<GameStateSnapshotter>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<GameStateSnapshotter>());

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
                return settings.Performance.EnableRegionalProcessing
                    ? provider.GetRequiredService<RegionalGameLoopStrategy>()
                    : (IGameLoopStrategy)provider.GetRequiredService<GlobalGameLoopStrategy>();
            });

            services.AddSingleton<GameLoop>();
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<GameLoop>());

            return services;
        }

        private static IServiceCollection AddServerApplicationServices(this IServiceCollection services)
        {
            services.AddSingleton<HttpServer>();
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<HttpServer>());

            services.AddSingleton<ServerApplication>();
            services.AddHostedService(provider => provider.GetRequiredService<ServerApplication>());

            return services;
        }
    }
}
