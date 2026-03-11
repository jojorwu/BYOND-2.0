using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Shared.Interfaces;
using Shared.Messaging;
using Shared.Services;

namespace Shared.Services;

public static class SharedServiceCollectionExtensions
{
    public static IServiceCollection AddSharedEngineServices(this IServiceCollection services)
    {
        services.AddSingleton<Shared.Config.IConfigurationManager, Shared.Config.ConfigurationManager>();
        services.AddSingleton<Shared.Config.IConsoleCommandManager, Shared.Config.ConsoleCommandManager>();
        services.AddCoreServices();
        services.AddEcsServices();
        services.AddNetworkingServices();
        services.AddJobSystem();

        return services;
    }

    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<IEngineManager, EngineManager>();
        services.AddSingleton<IEventBus, FastEventBus>();
        services.AddSingleton<ITimerService, TimerService>();
        services.AddSingleton<ProfilingService>();
        services.AddSingleton<IProfilingService>(sp => sp.GetRequiredService<ProfilingService>());
        services.AddSingleton<IShrinkable>(sp => sp.GetRequiredService<ProfilingService>());
        services.AddSingleton<ObjectTypeManager>();
        services.AddSingleton<IObjectTypeManager>(p => p.GetRequiredService<ObjectTypeManager>());
        services.AddSingleton<IEngineService>(p => p.GetRequiredService<ObjectTypeManager>());
        services.AddSingleton<StringInterner>();
        services.AddSingleton<IShrinkable>(sp => sp.GetRequiredService<StringInterner>());
        services.AddSingleton<IEntityRegistry, EntityRegistry>();
        services.AddSingleton<IObjectFactory, ObjectFactory>();
        services.AddSingleton<IArenaAllocator, ArenaProxy>();
        return services;
    }

    public static IServiceCollection AddEcsServices(this IServiceCollection services)
    {
        services.AddSingleton<SharedPool<GameObject>>(sp => new SharedPool<GameObject>(() => new GameObject()));
        services.AddSingleton<IObjectPool<GameObject>>(sp => sp.GetRequiredService<SharedPool<GameObject>>());
        services.AddSingleton<IShrinkable>(sp => sp.GetRequiredService<SharedPool<GameObject>>());

        services.AddSingleton<SharedPool<EntityCommandBuffer>>(sp => new SharedPool<EntityCommandBuffer>(() => new EntityCommandBuffer(sp.GetRequiredService<IObjectFactory>(), sp.GetRequiredService<IComponentManager>())));
        services.AddSingleton<IObjectPool<EntityCommandBuffer>>(sp => sp.GetRequiredService<SharedPool<EntityCommandBuffer>>());
        services.AddSingleton<IShrinkable>(sp => sp.GetRequiredService<SharedPool<EntityCommandBuffer>>());

        services.AddSingleton<ISystemRegistry, SystemRegistry>();
        services.AddSingleton<ISystemExecutionPlanner, SystemExecutionPlanner>();
        services.AddSingleton<ISystemManager, SystemManager>();
        services.AddSingleton<IArchetypeManager, ArchetypeManager>();
        services.AddSingleton<ComponentManager>();
        services.AddSingleton<IComponentManager>(sp => sp.GetRequiredService<ComponentManager>());
        services.AddSingleton<IShrinkable>(sp => sp.GetRequiredService<ComponentManager>());
        services.AddSingleton<IComponentQueryService>(sp => new ComponentQueryService(sp.GetRequiredService<IComponentManager>(), sp.GetService<IGameState>()));
        services.AddSingleton<IComponentMessageBus, ComponentMessageBus>();
        services.AddTransient<IEntityCommandBuffer, EntityCommandBuffer>();
        return services;
    }

    public static IServiceCollection AddNetworkingServices(this IServiceCollection services)
    {
        services.AddSingleton<IPluginManager, PluginManager>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddSingleton<LoggingPacketMiddleware>();
        services.AddSingleton<IPacketDispatcher>(sp =>
        {
            var dispatcher = new PacketDispatcher(sp.GetRequiredService<IJobSystem>());
            dispatcher.AddMiddleware(sp.GetRequiredService<LoggingPacketMiddleware>());
            return dispatcher;
        });
        services.AddSingleton<ISnapshotProvider, SnapshotProvider>();
        services.AddSingleton<BinarySnapshotService>();
        services.AddSingleton<SpatialGrid>();
        services.AddSingleton<IShrinkable>(sp => sp.GetRequiredService<SpatialGrid>());
        services.AddSingleton<IInterestManager, InterestManager>();
        return services;
    }

    public static IServiceCollection AddJobSystem(this IServiceCollection services)
    {
        services.AddSingleton<IComputeService, ComputeService>();
        services.AddSingleton<IJobSystem, JobSystem>();
        services.AddSingleton<SoundResourceProvider>();
        services.AddSingleton<IResourceSystem>(sp =>
        {
            var system = new ResourceSystem();
            system.RegisterProvider(sp.GetRequiredService<SoundResourceProvider>());
            return system;
        });
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

    /// <summary>
    /// Scans the given assembly for types implementing ISystem and registers them.
    /// </summary>
    public static IServiceCollection AddSystemsFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var systemTypes = assembly.GetTypes()
            .Where(t => typeof(ISystem).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in systemTypes)
        {
            services.AddSingleton(typeof(ISystem), type);
        }

        return services;
    }
}
