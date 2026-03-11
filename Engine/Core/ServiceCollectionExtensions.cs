using Core.Api;
using Core.Maps;
using Core.Players;
using Core.Regions;
using Core.VM.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared;
using Shared.Interfaces;
using Shared.Services;

namespace Core
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all core engine services, including state management, VM, scripting, and APIs.
        /// </summary>
        public static IServiceCollection AddCoreServices(this IServiceCollection services)
        {
            return services
                .AddSharedEngineServices()
                .AddCoreStateServices()
                .AddCoreApiServices()
                .AddCoreVmServices()
                .AddCoreScriptingServices()
                .AddCoreProjectServices();
        }

        private static IServiceCollection AddCoreStateServices(this IServiceCollection services)
        {
            services.AddSingleton<GameState>();
            services.AddSingleton<IGameState>(p => p.GetRequiredService<GameState>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<GameState>());

            services.AddSingleton<MapLoader>();
            services.AddSingleton<IMapLoader>(p => p.GetRequiredService<MapLoader>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<MapLoader>());

            services.AddSingleton<IPlayerManager, PlayerManager>();

            services.AddSingleton<IRegionActivationStrategy, PlayerBasedActivationStrategy>();
            services.AddSingleton<IRegionManager, RegionManager>();

            return services;
        }

        private static IServiceCollection AddCoreApiServices(this IServiceCollection services)
        {
            services.AddSingleton<Shared.Config.ISoundRegistry, Shared.Config.SoundRegistry>();
            services.AddSingleton<IMapApi, MapApi>();
            services.AddSingleton<IObjectApi, ObjectApi>();
            services.AddSingleton<IScriptApi, ScriptApi>();
            services.AddSingleton<IStandardLibraryApi, StandardLibraryApi>();
            services.AddSingleton<Shared.Api.ISpatialQueryApi, SpatialQueryApi>();
            services.AddSingleton<ITimeApi, TimeApi>();
            services.AddSingleton<IEventApi, EventApi>();
            services.AddSingleton<IGameApi, GameApi>();
            services.AddSingleton<IRegionApi, RegionApi>();
            services.AddSingleton<ISoundApi, SoundApi>();

            return services;
        }

        private static IServiceCollection AddCoreVmServices(this IServiceCollection services)
        {
            services.AddOptions<DreamVmConfiguration>().Configure<IOptions<ServerSettings>>((config, settings) =>
            {
                config.MaxInstructions = settings.Value.VmMaxInstructions;
            });

            services.AddSingleton<IBytecodeInterpreter, BytecodeInterpreter>();
            services.AddSingleton<INativeProcProvider, Core.VM.Procs.MathNativeProcProvider>();
            services.AddSingleton<INativeProcProvider, Core.VM.Procs.SpatialNativeProcProvider>();
            services.AddSingleton<INativeProcProvider>(p => new Core.VM.Procs.SystemNativeProcProvider(p.GetService<ISoundApi>()));
            services.AddSingleton<DreamVM>();
            services.AddSingleton<IDreamVM>(provider => provider.GetRequiredService<DreamVM>());
            services.AddSingleton<IEngineService>(provider => provider.GetRequiredService<DreamVM>());

            services.AddSingleton<ITypeSystemPopulator, TypeSystemPopulator>();
            services.AddSingleton<CompiledJsonService>();
            services.AddSingleton<ICompiledJsonService>(p => p.GetRequiredService<CompiledJsonService>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<CompiledJsonService>());

            services.AddSingleton<DreamMakerLoader>();
            services.AddSingleton<IDreamMakerLoader>(p => p.GetRequiredService<DreamMakerLoader>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<DreamMakerLoader>());

            return services;
        }

        private static IServiceCollection AddCoreScriptingServices(this IServiceCollection services)
        {
            services.AddSingleton<ScriptManager>();
            services.AddSingleton<IScriptManager>(p => p.GetRequiredService<ScriptManager>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<ScriptManager>());

            services.AddSingleton<IScriptSystem, Core.Scripting.CSharp.CSharpSystem>();
            services.AddSingleton<IScriptSystem, Core.Scripting.LuaSystem.LuaSystem>();
            services.AddSingleton<IScriptSystem>(provider =>
                new Core.Scripting.DM.DmSystem(
                    provider.GetRequiredService<IObjectTypeManager>(),
                    provider.GetRequiredService<IDreamMakerLoader>(),
                    provider.GetRequiredService<IDreamVM>(),
                    new Lazy<IScriptHost>(() => provider.GetRequiredService<IScriptHost>()),
                    provider.GetRequiredService<ILogger<Core.Scripting.DM.DmSystem>>()
                )
            );

            return services;
        }

        private static IServiceCollection AddCoreProjectServices(this IServiceCollection services)
        {
            services.AddSingleton<IProjectManager, Core.Projects.ProjectManager>();
            services.AddSingleton<IServerDiscoveryService, Core.Networking.MockServerDiscoveryService>();
            return services;
        }
    }
}
