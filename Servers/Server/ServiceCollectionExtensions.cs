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
        public static IServiceCollection AddServerHostedServices(this IServiceCollection services)
        {
            // Diagnostics
            services.AddSingleton<PerformanceMonitor>();
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<PerformanceMonitor>());

            // Core Server Context
            services.AddSingleton<IServerContext, ServerContext>();

            // Scripting logic
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

            // Networking
            services.AddSingleton<NetDataWriterPool>();
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<NetDataWriterPool>());
            services.AddSingleton<NetworkService>();
            services.AddSingleton<INetworkService>(p => p.GetRequiredService<NetworkService>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<NetworkService>());
            services.AddSingleton<NetworkEventHandler>();
            services.AddSingleton<UdpServer>();
            services.AddSingleton<IUdpServer>(provider => provider.GetRequiredService<UdpServer>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<UdpServer>());

            // Game Loop strategies
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
                if (settings.Performance.EnableRegionalProcessing)
                {
                    return provider.GetRequiredService<RegionalGameLoopStrategy>();
                }
                else
                {
                    return provider.GetRequiredService<GlobalGameLoopStrategy>();
                }
            });

            // Services that will be managed by ServerApplication
            services.AddSingleton<GameLoop>();
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<GameLoop>());
            services.AddSingleton<HttpServer>();
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<HttpServer>());

            // The main application coordinator
            services.AddSingleton<ServerApplication>();
            services.AddHostedService(provider => provider.GetRequiredService<ServerApplication>());

            return services;
        }
    }
}
