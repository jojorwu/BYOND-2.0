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
        services.AddSingleton<ILifecycleOrchestrator, DefaultLifecycleOrchestrator>();
        services.AddSingleton<FastEventBus>();
        services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<FastEventBus>());
        services.AddSingleton<IEngineService>(sp => sp.GetRequiredService<FastEventBus>());
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
        services.AddSingleton<SystemManager>();
        services.AddSingleton<ISystemManager>(sp => sp.GetRequiredService<SystemManager>());
        services.AddSingleton<ITickable>(sp => sp.GetRequiredService<SystemManager>());
        services.AddSingleton<IEngineService>(sp => sp.GetRequiredService<SystemManager>());
        services.AddSingleton<ArchetypeManager>();
        services.AddSingleton<IArchetypeManager>(sp => sp.GetRequiredService<ArchetypeManager>());
        services.AddSingleton<IEngineService>(sp => sp.GetRequiredService<ArchetypeManager>());
        services.AddSingleton<ComponentManager>();
        services.AddSingleton<IComponentManager>(sp => sp.GetRequiredService<ComponentManager>());
        services.AddSingleton<IShrinkable>(sp => sp.GetRequiredService<ComponentManager>());
        services.AddSingleton<IComponentQueryService>(sp => new ComponentQueryService(sp.GetRequiredService<IComponentManager>(), sp.GetService<IGameState>()));
        services.AddSingleton<IComponentMessageBus, ComponentMessageBus>();
        services.AddSystem<Systems.StateCommitSystem>();
        services.AddTransient<IEntityCommandBuffer, EntityCommandBuffer>();
        return services;
    }

    public static IServiceCollection AddNetworkingServices(this IServiceCollection services)
    {
        services.AddSingleton<DiagnosticBus>();
        services.AddSingleton<IDiagnosticBus>(sp => sp.GetRequiredService<DiagnosticBus>());
        services.AddSingleton<IEngineService>(sp => sp.GetRequiredService<DiagnosticBus>());
        services.AddSingleton<IPluginManager, PluginManager>();
        services.AddSingleton<ICommandHistoryService, CommandHistoryService>();
        services.AddSingleton<ICommandMiddleware, LoggingCommandMiddleware>();
        services.AddSingleton<ICommandMiddleware, DiagnosticCommandMiddleware>();
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
        services.AddSingleton<IShrinkable>(sp => sp.GetRequiredService<BinarySnapshotService>());
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
        services.AddSingleton<ResourceSystem>(sp =>
        {
            var system = new ResourceSystem();
            system.RegisterProvider(sp.GetRequiredService<SoundResourceProvider>());
            return system;
        });
        services.AddSingleton<IResourceSystem>(sp => sp.GetRequiredService<ResourceSystem>());
        services.AddSingleton<IShrinkable>(sp => sp.GetRequiredService<ResourceSystem>());
        return services;
    }

    /// <summary>
    /// Scans the given assembly for types implementing IEngineService and registers them as singletons.
    /// Also registers them under all implemented interfaces from the Shared.Interfaces namespace.
    /// </summary>
    public static IServiceCollection AddEngineServicesFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var serviceTypes = assembly.GetTypes()
            .Where(t => typeof(IEngineService).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in serviceTypes)
        {
            services.AddSingleton(type);
            services.AddSingleton(typeof(IEngineService), sp => sp.GetRequiredService(type));

            var interfaces = type.GetInterfaces();
            foreach (var @interface in interfaces)
            {
                if (@interface.Namespace == "Shared.Interfaces" && @interface != typeof(IEngineService))
                {
                    services.AddSingleton(@interface, sp => sp.GetRequiredService(type));
                }
            }
        }

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
