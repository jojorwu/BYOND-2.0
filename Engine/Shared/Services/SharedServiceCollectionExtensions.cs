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
            services.AddSingleton<IObjectPool<GameObject>>(sp => new SharedPool<GameObject>(() => new GameObject()));
            services.AddSingleton<ISystemRegistry, SystemRegistry>();
            services.AddSingleton<IPluginManager, PluginManager>();
            services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
            services.AddSingleton<IPacketDispatcher, PacketDispatcher>();
            services.AddSingleton<IProfilingService, ProfilingService>();
            services.AddSingleton<ISnapshotProvider, SnapshotProvider>();
            services.AddSingleton<BinarySnapshotService>();
            services.AddSingleton<StringInterner>();
            services.AddSingleton<IComponentManager, ComponentManager>();
            services.AddSingleton<IComponentQueryService, ComponentQueryService>();
            services.AddSingleton<IObjectFactory, ObjectFactory>();
            services.AddSingleton<IInterestManager, InterestManager>();
            services.AddTransient<IEntityCommandBuffer, EntityCommandBuffer>();
            return services;
        }
    }
}
