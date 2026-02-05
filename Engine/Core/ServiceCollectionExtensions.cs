using Core.Api;
using Core.Maps;
using Core.Objects;
using Core.Players;
using Core.Regions;
using Core.VM.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Interfaces;

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
                .AddCoreStateServices()
                .AddCoreApiServices()
                .AddCoreVmServices()
                .AddCoreScriptingServices()
                .AddCoreProjectServices();
        }

        private static IServiceCollection AddCoreStateServices(this IServiceCollection services)
        {
            services.AddSingleton<IGameState, GameState>();

            services.AddSingleton<ObjectTypeManager>();
            services.AddSingleton<IObjectTypeManager>(p => p.GetRequiredService<ObjectTypeManager>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<ObjectTypeManager>());

            services.AddSingleton<MapLoader>();
            services.AddSingleton<IMapLoader>(p => p.GetRequiredService<MapLoader>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<MapLoader>());

            services.AddSingleton<IPlayerManager>(provider =>
                new PlayerManager(
                    provider.GetRequiredService<IObjectApi>(),
                    provider.GetRequiredService<IObjectTypeManager>(),
                    provider.GetRequiredService<ServerSettings>()
                )
            );

            services.AddSingleton<IRegionActivationStrategy, PlayerBasedActivationStrategy>();
            services.AddSingleton<IRegionManager>(provider =>
                new RegionManager(
                    provider.GetRequiredService<IMap>(),
                    provider.GetRequiredService<ServerSettings>()
                )
            );

            return services;
        }

        private static IServiceCollection AddCoreApiServices(this IServiceCollection services)
        {
            services.AddSingleton<IMapApi, MapApi>();
            services.AddSingleton<IObjectApi, ObjectApi>();
            services.AddSingleton<IScriptApi, ScriptApi>();
            services.AddSingleton<IStandardLibraryApi, StandardLibraryApi>();
            services.AddSingleton<Shared.Api.ISpatialQueryApi, SpatialQueryApi>();
            services.AddSingleton<IGameApi, GameApi>();
            services.AddSingleton<IRegionApi>(provider =>
                new RegionApi(
                    provider.GetRequiredService<IRegionManager>(),
                    provider.GetRequiredService<IRegionActivationStrategy>(),
                    provider.GetRequiredService<ServerSettings>()
                )
            );

            return services;
        }

        private static IServiceCollection AddCoreVmServices(this IServiceCollection services)
        {
            services.AddSingleton<DreamVM>();
            services.AddSingleton<IDreamVM>(provider => provider.GetRequiredService<DreamVM>());

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
            services.AddSingleton(provider =>
                new ScriptManager(
                    provider.GetRequiredService<IProject>(),
                    provider.GetServices<IScriptSystem>()
                )
            );
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
