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
    }
}
