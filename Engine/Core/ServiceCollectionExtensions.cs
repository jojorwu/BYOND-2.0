using Core.Api;
using Core.Maps;
using Core.Objects;
using Core.Players;
using Core.Regions;
using Core.VM.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;

namespace Core
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCoreServices(this IServiceCollection services)
        {
            services.AddSingleton<IGameState, GameState>();
            services.AddSingleton<IObjectTypeManager, ObjectTypeManager>();
            services.AddSingleton<IMapLoader, MapLoader>();
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
            services.AddSingleton<DreamVM>();
            services.AddSingleton<IDreamVM>(provider => provider.GetRequiredService<DreamVM>());
            services.AddSingleton<ICompiledJsonService>(provider => new CompiledJsonService(provider.GetService<ILogger<CompiledJsonService>>()));
            services.AddSingleton<IDreamMakerLoader, DreamMakerLoader>();
            services.AddSingleton<IScriptManager>(provider =>
                new ScriptManager(
                    provider.GetRequiredService<IProject>(),
                    provider.GetServices<IScriptSystem>()
                )
            );
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

            services.AddSingleton<IProjectManager, Core.Projects.ProjectManager>();
            services.AddSingleton<IServerDiscoveryService, Core.Networking.MockServerDiscoveryService>();

            return services;
        }
    }
}
