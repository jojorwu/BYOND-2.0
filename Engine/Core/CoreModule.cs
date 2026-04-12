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
using System;

namespace Core
{
    public class CoreModule : IEngineModule
    {
        public void RegisterServices(IServiceCollection services)
        {
            AddCoreStateServices(services);
            AddCoreApiServices(services);
            AddCoreVmServices(services);
            AddCoreScriptingServices(services);
            AddCoreProjectServices(services);
        }

        private void AddCoreStateServices(IServiceCollection services)
        {
            // GameState, MapLoader, PlayerManager, RegionManager, and ActivationStrategy are auto-registered via [EngineService] attribute
        }

        private void AddCoreApiServices(IServiceCollection services)
        {
            services.AddSingleton<Shared.Config.ISoundRegistry, Shared.Config.SoundRegistry>();
            services.AddSingleton<IApiRegistry, ApiRegistry>();

            // API services are now auto-registered via [EngineService] attribute

            services.AddSingleton<Shared.Api.ISpatialQueryApi, SpatialQueryApi>();

            services.AddSingleton<IGameApi>(sp =>
            {
                var registry = sp.GetRequiredService<IApiRegistry>();
                registry.RegisterAll(sp);

                return new GameApi(
                    registry,
                    sp.GetRequiredService<Shared.Config.ISoundRegistry>(),
                    sp.GetRequiredService<Shared.Config.IConsoleCommandManager>()
                );
            });
        }

        private void AddCoreVmServices(IServiceCollection services)
        {
            services.AddOptions<DreamVmConfiguration>().Configure<IOptions<ServerSettings>>((config, settings) =>
            {
                config.MaxInstructions = settings.Value.VmMaxInstructions;
            });

            services.AddSingleton<IBytecodeInterpreter, BytecodeInterpreter>();
            services.AddSingleton<INativeProcProvider, Core.VM.Procs.MathNativeProcProvider>();
            services.AddSingleton<INativeProcProvider, Core.VM.Procs.SpatialNativeProcProvider>();
            services.AddSingleton<INativeProcProvider>(p => new Core.VM.Procs.SystemNativeProcProvider(p.GetService<ISoundApi>(), p.GetService<IScriptBridge>()));

            // DreamVM is auto-registered via [EngineService] attribute

            services.AddSingleton<ITypeSystemPopulator, TypeSystemPopulator>();
            services.AddSingleton<CompiledJsonService>();
            services.AddSingleton<ICompiledJsonService>(p => p.GetRequiredService<CompiledJsonService>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<CompiledJsonService>());

            services.AddSingleton<DreamMakerLoader>();
            services.AddSingleton<IDreamMakerLoader>(p => p.GetRequiredService<DreamMakerLoader>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<DreamMakerLoader>());
        }

        private void AddCoreScriptingServices(IServiceCollection services)
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
                    provider.GetRequiredService<ILogger<Core.Scripting.DM.DmSystem>>(),
                    provider.GetRequiredService<IScriptBridge>()
                )
            );
        }

        private void AddCoreProjectServices(IServiceCollection services)
        {
            services.AddSingleton<IProjectManager, Core.Projects.ProjectManager>();
            services.AddSingleton<IServerDiscoveryService, Core.Networking.MockServerDiscoveryService>();
        }

        public void PreTick() { }
        public void PostTick() { }
    }
}
