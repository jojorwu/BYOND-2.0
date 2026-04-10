using Core;
using Core.VM.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared;
using Shared.Interfaces;
using Shared.Services;
using System;
using System.Collections.Generic;

namespace Server
{
    public class ServerModule : IEngineModule
    {
        public void RegisterServices(IServiceCollection services)
        {
            // Configuration and Commands are now auto-registered via [EngineService]

            services.AddSingleton(resolver => {
                var manager = resolver.GetRequiredService<Shared.Config.IConfigurationManager>();
                return new ServerSettings(manager);
            });

            services.AddSingleton<IProject>(new Project("."));
            services.AddSingleton<Shared.IJsonService, DMCompiler.Json.JsonService>();
            services.AddSingleton<Shared.ICompilerService, DMCompiler.CompilerService>();

            // PerformanceMonitor, ScriptHost, NetDataWriterPool, NetworkService, UdpServer,
            // GameStateSnapshotter, HttpServer are auto-registered via [EngineService] attribute

            services.AddSingleton<IServerContext, ServerContext>();
            services.AddSingleton<CVarReplicator>(sp =>
                new CVarReplicator(
                    sp.GetRequiredService<Shared.Config.IConfigurationManager>(),
                    sp.GetRequiredService<NetDataWriterPool>(),
                    sp.GetRequiredService<IServerContext>().PlayerManager
                ));

            services.AddSingleton<IScriptWatcher, ScriptWatcher>();
            services.AddSingleton<IScriptEnvironmentManager, ScriptEnvironmentManager>();
            services.AddSingleton<IScriptScheduler, ScriptScheduler>();
            services.AddSingleton<IScriptCommandProcessor, ScriptCommandProcessor>();

            services.AddSingleton<NetworkEventHandler>(sp =>
                new NetworkEventHandler(
                    sp.GetRequiredService<INetworkService>(),
                    sp.GetRequiredService<IServerContext>(),
                    sp.GetRequiredService<IScriptHost>(),
                    sp.GetRequiredService<IUdpServer>(),
                    sp.GetRequiredService<Shared.Config.IConsoleCommandManager>(),
                    sp.GetRequiredService<ILogger<NetworkEventHandler>>()
                ));

            services.AddSingleton(provider => new GlobalGameLoopStrategy(
                provider.GetRequiredService<IScriptHost>(),
                provider.GetRequiredService<IGameState>(),
                provider.GetRequiredService<IGameStateSnapshotter>(),
                provider.GetRequiredService<IUdpServer>()
            ));

            services.AddSingleton<RegionalGameLoopStrategy>();
            services.AddSingleton<IShrinkable>(p => p.GetRequiredService<RegionalGameLoopStrategy>());

            services.AddSingleton<IGameLoopStrategy>(provider =>
            {
                var settings = provider.GetRequiredService<ServerSettings>();
                return settings.Performance.EnableRegionalProcessing
                    ? provider.GetRequiredService<RegionalGameLoopStrategy>()
                    : (IGameLoopStrategy)provider.GetRequiredService<GlobalGameLoopStrategy>();
            });

            services.AddSingleton<GameLoop>(sp => new GameLoop(
                sp.GetRequiredService<IGameLoopStrategy>(),
                sp,
                sp.GetRequiredService<ITimerService>(),
                sp.GetRequiredService<IServerContext>(),
                sp.GetRequiredService<ILogger<GameLoop>>()
            ));
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<GameLoop>());

            services.AddSingleton<ServerApplication>();
            services.AddSingleton<IEngine>(sp => sp.GetRequiredService<ServerApplication>());
            services.AddHostedService(provider => provider.GetRequiredService<ServerApplication>());
        }

        public void PreTick() { }
        public void PostTick() { }
    }
}
