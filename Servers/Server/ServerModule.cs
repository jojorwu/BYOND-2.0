using Microsoft.Extensions.DependencyInjection;
using Shared.Interfaces;
using Shared.Models;
using Shared.Services;
using Server.Systems;
using Shared;
using Core;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using System;

namespace Server
{
    public class ServerModule : BaseModule
    {
        public override string Name => "ServerCore";

        public override void RegisterServices(IServiceCollection services)
        {
            // Diagnostics
            services.AddSingleton<PerformanceMonitor>();
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<PerformanceMonitor>());

            // Core Server Services
            services.AddSingleton<IServerContext, ServerContext>();
            services.AddSingleton<IScriptWatcher, ScriptWatcher>();
            services.AddSingleton<IScriptEnvironmentManager, ScriptEnvironmentManager>();
            services.AddSingleton<IScriptScheduler, ScriptScheduler>();
            services.AddSingleton<IScriptCommandProcessor, ScriptCommandProcessor>();

            services.AddSingleton<ScriptHost>();
            services.AddSingleton<IScriptHost>(provider => provider.GetRequiredService<ScriptHost>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<ScriptHost>());

            // Networking
            services.AddSingleton<NetDataWriterPool>();
            services.AddSingleton<IShrinkable>(p => p.GetRequiredService<NetDataWriterPool>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<NetDataWriterPool>());

            services.AddSingleton<NetworkService>();
            services.AddSingleton<INetworkService>(p => p.GetRequiredService<NetworkService>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<NetworkService>());

            services.AddSingleton<NetworkEventHandler>();

            services.AddSingleton<UdpServer>();
            services.AddSingleton<IUdpServer>(provider => provider.GetRequiredService<UdpServer>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<UdpServer>());

            // Game Loop
            services.AddSingleton<GameStateSnapshotter>();
            services.AddSingleton<IGameStateSnapshotter>(p => p.GetRequiredService<GameStateSnapshotter>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<GameStateSnapshotter>());

            services.AddSingleton<RegionalGameLoopStrategy>();
            services.AddSingleton<ISystem>(p => p.GetRequiredService<RegionalGameLoopStrategy>());
            services.AddSingleton<IShrinkable>(p => p.GetRequiredService<RegionalGameLoopStrategy>());

            services.AddSingleton<GameLoop>();
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<GameLoop>());

            // HTTP Server
            services.AddSingleton<HttpServer>();
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<HttpServer>());

            // Systems
            services.AddSystem<ScriptSystem>();
            services.AddSystem<NetworkingSystem>();
            services.AddSystem<StateCommitSystem>();
        }

        public override async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            await base.InitializeAsync(serviceProvider);
            // Any specific initialization logic for the server module
        }
    }
}
