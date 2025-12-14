using Core;
using Core.VM.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace Server
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
            services.AddSingleton<IGameApi, GameApi>();
            services.AddSingleton<IRegionApi, RegionApi>();
            services.AddSingleton<IDmmService, DmmService>();
            services.AddSingleton<ICompilerService, OpenDreamCompilerService>();
            services.AddSingleton<DreamVM>();
            services.AddSingleton<IDreamVM>(provider => provider.GetRequiredService<DreamVM>());
            services.AddSingleton<IDreamMakerLoader, DreamMakerLoader>();
            services.AddSingleton<IScriptManager>(provider =>
                new ScriptManager(
                    provider.GetRequiredService<IProject>(),
                    provider.GetServices<IScriptSystem>(),
                    provider.GetRequiredService<IGameState>()
                )
            );
            services.AddSingleton<IPlayerManager>(provider =>
                new Core.PlayerManager(
                    provider.GetRequiredService<IObjectApi>(),
                    provider.GetRequiredService<IObjectTypeManager>(),
                    provider.GetRequiredService<ServerSettings>()
                )
            );
            services.AddSingleton<IRegionManager>(provider =>
                new RegionManager(
                    provider.GetRequiredService<IMap>(),
                    provider.GetRequiredService<IScriptHost>(),
                    provider.GetRequiredService<IGameState>(),
                    provider.GetRequiredService<IPlayerManager>(),
                    provider.GetRequiredService<ServerSettings>()
                )
            );
            services.AddSingleton<IScriptSystem, Core.Scripting.CSharp.CSharpSystem>();
            services.AddSingleton<IScriptSystem, Core.Scripting.LuaSystem.LuaSystem>();
            services.AddSingleton<IScriptSystem>(provider =>
                new Core.Scripting.DM.DmSystem(
                    provider.GetRequiredService<IObjectTypeManager>(),
                    provider.GetRequiredService<IDreamMakerLoader>(),
                    provider.GetRequiredService<ICompilerService>(),
                    provider.GetRequiredService<IDreamVM>(),
                    () => provider.GetRequiredService<IScriptHost>()
                )
            );

            return services;
        }

        public static IServiceCollection AddServerHostedServices(this IServiceCollection services)
        {
            services.AddSingleton<ScriptHost>();
            services.AddSingleton<IScriptHost>(provider => provider.GetRequiredService<ScriptHost>());
            services.AddHostedService(provider => provider.GetRequiredService<ScriptHost>());

            services.AddSingleton<INetworkService, NetworkService>();
            services.AddSingleton<NetworkEventHandler>();
            services.AddSingleton<UdpServer>();
            services.AddSingleton<IUdpServer>(provider => provider.GetRequiredService<UdpServer>());
            services.AddHostedService(provider => provider.GetRequiredService<UdpServer>());

            services.AddSingleton<GlobalGameLoopStrategy>();
            services.AddSingleton(provider =>
                new RegionalGameLoopStrategy(
                    provider.GetRequiredService<IScriptHost>(),
                    provider.GetRequiredService<IRegionManager>(),
                    provider.GetRequiredService<IUdpServer>(),
                    provider.GetRequiredService<IGameState>(),
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
