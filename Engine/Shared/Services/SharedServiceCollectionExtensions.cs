using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Shared.Interfaces;
using Shared.Messaging;
using Shared.Services;

namespace Shared.Services
{
    public static class SharedServiceCollectionExtensions
    {
        public static IServiceCollection AddSharedEngineServices(this IServiceCollection services)
        {
            services.AddSingleton<IEngineManager, EngineManager>();
            services.AddSingleton<IEventBus, EventBus>();
            services.AddSingleton<IComputeService, ComputeService>();
            services.AddSingleton<IJobSystem, JobSystem>();
            services.AddSingleton<ITimerService, TimerService>();
            services.AddSingleton<SharedPool<GameObject>>(sp => new SharedPool<GameObject>(() => new GameObject()));
            services.AddSingleton<IObjectPool<GameObject>>(sp => sp.GetRequiredService<SharedPool<GameObject>>());
            services.AddSingleton<IShrinkable>(sp => sp.GetRequiredService<SharedPool<GameObject>>());

            services.AddSingleton<SharedPool<EntityCommandBuffer>>(sp => new SharedPool<EntityCommandBuffer>(() => new EntityCommandBuffer(sp.GetRequiredService<IObjectFactory>(), sp.GetRequiredService<IComponentManager>())));
            services.AddSingleton<IObjectPool<EntityCommandBuffer>>(sp => sp.GetRequiredService<SharedPool<EntityCommandBuffer>>());
            services.AddSingleton<IShrinkable>(sp => sp.GetRequiredService<SharedPool<EntityCommandBuffer>>());

            services.AddSingleton<ISystemRegistry, SystemRegistry>();
            services.AddSingleton<ISystemManager, SystemManager>();
            services.AddSingleton<ICommandRegistry, CommandRegistry>();
            services.AddSingleton<IPluginManager, PluginManager>();
            services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
            services.AddSingleton<IPacketDispatcher, PacketDispatcher>();
            services.AddSingleton<ProfilingService>();
            services.AddSingleton<IProfilingService>(sp => sp.GetRequiredService<ProfilingService>());
            services.AddSingleton<IShrinkable>(sp => sp.GetRequiredService<ProfilingService>());
            services.AddSingleton<ISnapshotProvider, SnapshotProvider>();
            services.AddSingleton<BinarySnapshotService>();
            services.AddSingleton<ObjectTypeManager>();
            services.AddSingleton<IObjectTypeManager>(p => p.GetRequiredService<ObjectTypeManager>());
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<ObjectTypeManager>());
            services.AddSingleton<SpatialGrid>();
            services.AddSingleton<IShrinkable>(sp => sp.GetRequiredService<SpatialGrid>());
            services.AddSingleton<StringInterner>();
            services.AddSingleton<IShrinkable>(sp => sp.GetRequiredService<StringInterner>());
            services.AddSingleton<IArchetypeManager, ArchetypeManager>();
            services.AddSingleton<ComponentManager>();
            services.AddSingleton<IComponentManager>(sp => sp.GetRequiredService<ComponentManager>());
            services.AddSingleton<IShrinkable>(sp => sp.GetRequiredService<ComponentManager>());
            services.AddSingleton<IComponentQueryService>(sp => new ComponentQueryService(sp.GetRequiredService<IComponentManager>(), sp.GetService<IGameState>()));
            services.AddSingleton<IComponentMessageBus, ComponentMessageBus>();
            services.AddSingleton<IObjectFactory, ObjectFactory>();
            services.AddSingleton<IInterestManager, InterestManager>();
            services.AddTransient<IEntityCommandBuffer, EntityCommandBuffer>();
            services.AddSingleton<IArenaAllocator, ArenaProxy>();
            return services;
        }

        /// <summary>
        /// Registers an engine module and its associated services.
        /// </summary>
        public static IServiceCollection AddEngineModule<T>(this IServiceCollection services) where T : class, IEngineModule, new()
        {
            var module = new T();
            services.AddSingleton<IEngineModule>(module);
            module.RegisterServices(services);
            return services;
        }

        /// <summary>
        /// Scans the given assembly for types implementing IEngineModule and registers them.
        /// </summary>
        public static IServiceCollection AddEngineModulesFromAssembly(this IServiceCollection services, Assembly assembly)
        {
            var moduleTypes = assembly.GetTypes()
                .Where(t => typeof(IEngineModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in moduleTypes)
            {
                if (Activator.CreateInstance(type) is IEngineModule module)
                {
                    services.AddSingleton(typeof(IEngineModule), module);
                    module.RegisterServices(services);
                }
            }

            return services;
        }

        /// <summary>
        /// Registers a system in the DI container so it can be automatically discovered by the SystemManager.
        /// </summary>
        public static IServiceCollection AddSystem<T>(this IServiceCollection services) where T : class, ISystem
        {
            services.AddSingleton<ISystem, T>();
            return services;
        }
    }
}
