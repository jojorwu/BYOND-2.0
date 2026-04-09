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
            services.AddSingleton<Shared.Config.IConfigurationManager>(sp => {
                var manager = new Shared.Config.ConfigurationManager();
                manager.RegisterFromAssemblies(typeof(Shared.Config.ConfigKeys).Assembly);
                manager.AddProvider(new Shared.Config.JsonConfigProvider("server_config.json"));
                manager.AddProvider(new Shared.Config.EnvironmentConfigProvider());
                manager.LoadAll();
                return manager;
            });

            services.AddSingleton<Shared.Config.IConsoleCommandManager>(sp => {
                var manager = new Shared.Config.ConsoleCommandManager();
                var config = sp.GetRequiredService<Shared.Config.IConfigurationManager>();
                var soundApi = sp.GetRequiredService<ISoundApi>();
                manager.RegisterCommand(new Shared.Config.CVarListCommand(config));
                manager.RegisterCommand(new Shared.Config.CVarSetCommand(config));
                var settings = sp.GetRequiredService<ServerSettings>();
                var playerManager = sp.GetRequiredService<IPlayerManager>();
                manager.RegisterCommand(new Shared.Config.HelpCommand(manager));
                manager.RegisterCommand(new Shared.Config.SoundPlayCommand(soundApi));
                manager.RegisterCommand(new Shared.Config.StatusCommand(settings.ServerName, settings.MaxPlayers, playerManager));
                manager.RegisterCommand(new Shared.Config.PlayerListCommand(playerManager));
                return manager;
            });

            services.AddSingleton(resolver => {
                var manager = resolver.GetRequiredService<Shared.Config.IConfigurationManager>();
                return new ServerSettings(manager);
            });

            services.AddSingleton<IProject>(new Project("."));
            services.AddSingleton<Shared.IJsonService, DMCompiler.Json.JsonService>();
            services.AddSingleton<Shared.ICompilerService, DMCompiler.CompilerService>();

            services.AddSingleton<PerformanceMonitor>();
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<PerformanceMonitor>());

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

            services.AddSingleton<ScriptHost>();
            services.AddSingleton<IScriptHost>(provider => provider.GetRequiredService<ScriptHost>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<ScriptHost>());

            services.AddSingleton<ISystemManager, SystemManager>();

            services.AddSingleton<NetDataWriterPool>();
            services.AddSingleton<IShrinkable>(p => p.GetRequiredService<NetDataWriterPool>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<NetDataWriterPool>());

            services.AddSingleton<NetworkService>();
            services.AddSingleton<INetworkService>(p => p.GetRequiredService<NetworkService>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<NetworkService>());

            services.AddSingleton<NetworkEventHandler>(sp =>
                new NetworkEventHandler(
                    sp.GetRequiredService<INetworkService>(),
                    sp.GetRequiredService<IServerContext>(),
                    sp.GetRequiredService<IScriptHost>(),
                    sp.GetRequiredService<IUdpServer>(),
                    sp.GetRequiredService<Shared.Config.IConsoleCommandManager>(),
                    sp.GetRequiredService<ILogger<NetworkEventHandler>>()
                ));

            services.AddSingleton<UdpServer>();
            services.AddSingleton<IUdpServer>(provider => provider.GetRequiredService<UdpServer>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<UdpServer>());

            services.AddSingleton<GameStateSnapshotter>();
            services.AddSingleton<IGameStateSnapshotter>(p => p.GetRequiredService<GameStateSnapshotter>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<GameStateSnapshotter>());

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

            services.AddSingleton<HttpServer>();
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<HttpServer>());

            services.AddSingleton<ServerApplication>();
            services.AddSingleton<IEngine>(sp => sp.GetRequiredService<ServerApplication>());
            services.AddHostedService(provider => provider.GetRequiredService<ServerApplication>());
        }

        public void PreTick() { }
        public void PostTick() { }
    }
}
