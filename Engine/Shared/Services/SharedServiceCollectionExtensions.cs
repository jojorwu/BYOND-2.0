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
    /// <summary>
    /// Registers a service as a singleton implementation and also under one or more interfaces.
    /// This ensures that the same singleton instance is returned regardless of which interface is used for resolution.
    /// </summary>
    public static IServiceCollection AddEngineService<TImplementation>(this IServiceCollection services, params Type[] interfaceTypes)
        where TImplementation : class, IEngineService
    {
        services.AddSingleton<TImplementation>();
        services.AddSingleton<IEngineService>(sp => sp.GetRequiredService<TImplementation>());
        if (typeof(IEngineLifecycle).IsAssignableFrom(typeof(TImplementation)))
        {
            services.AddSingleton<IEngineLifecycle>(sp => (IEngineLifecycle)sp.GetRequiredService<TImplementation>());
        }
        if (typeof(ITickable).IsAssignableFrom(typeof(TImplementation)))
        {
            services.AddSingleton<ITickable>(sp => (ITickable)sp.GetRequiredService<TImplementation>());
        }
        if (typeof(IShrinkable).IsAssignableFrom(typeof(TImplementation)))
        {
            services.AddSingleton<IShrinkable>(sp => (IShrinkable)sp.GetRequiredService<TImplementation>());
        }
        if (typeof(IFreezable).IsAssignableFrom(typeof(TImplementation)))
        {
            services.AddSingleton<IFreezable>(sp => (IFreezable)sp.GetRequiredService<TImplementation>());
        }
        foreach (var type in interfaceTypes)
        {
            services.AddSingleton(type, sp => sp.GetRequiredService<TImplementation>());
        }
        return services;
    }
    /// <summary>
    /// Registers a service as a singleton implementation and also under one or more interfaces.
    /// Allows providing a factory for the implementation.
    /// </summary>
    public static IServiceCollection AddEngineService<TImplementation>(this IServiceCollection services, Func<IServiceProvider, TImplementation> factory, params Type[] interfaceTypes)
        where TImplementation : class, IEngineService
    {
        services.AddSingleton<TImplementation>(factory);
        services.AddSingleton<IEngineService>(sp => sp.GetRequiredService<TImplementation>());
        if (typeof(IEngineLifecycle).IsAssignableFrom(typeof(TImplementation)))
        {
            services.AddSingleton<IEngineLifecycle>(sp => (IEngineLifecycle)sp.GetRequiredService<TImplementation>());
        }
        if (typeof(ITickable).IsAssignableFrom(typeof(TImplementation)))
        {
            services.AddSingleton<ITickable>(sp => (ITickable)sp.GetRequiredService<TImplementation>());
        }
        if (typeof(IShrinkable).IsAssignableFrom(typeof(TImplementation)))
        {
            services.AddSingleton<IShrinkable>(sp => (IShrinkable)sp.GetRequiredService<TImplementation>());
        }
        if (typeof(IFreezable).IsAssignableFrom(typeof(TImplementation)))
        {
            services.AddSingleton<IFreezable>(sp => (IFreezable)sp.GetRequiredService<TImplementation>());
        }
        foreach (var type in interfaceTypes)
        {
            services.AddSingleton(type, sp => sp.GetRequiredService<TImplementation>());
        }
        return services;
    }
    public static IServiceCollection AddSharedEngineServices(this IServiceCollection services)
    {
        services.AddEngineService<Shared.Config.ConfigurationManager>(typeof(Shared.Config.IConfigurationManager));
        services.AddEngineService<Shared.Config.ConsoleCommandManager>(typeof(Shared.Config.IConsoleCommandManager));
        services.AddCoreServices();
        services.AddEcsServices();
        services.AddNetworkingServices();
        services.AddJobSystem();
        return services;
    }
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddEngineService<ScriptBridge>(typeof(IScriptBridge));
        services.AddSingleton<IArenaAllocator, ArenaAllocator>();
        services.AddEngineService<ReactiveStateSystem>(typeof(IVariableChangeListener));
        services.AddEngineService<DiagnosticBus>(typeof(IDiagnosticBus));
        services.AddEngineService<LauncherPathProvider>(typeof(ILauncherPathProvider));
        services.AddEngineService<EngineManager>(typeof(IEngineManager));
        services.AddSingleton<ILifecycleOrchestrator, DefaultLifecycleOrchestrator>();
        services.AddEngineService<FastEventBus>(typeof(IEventBus));
        services.AddEngineService<TimerService>(typeof(ITimerService));
        services.AddEngineService<ProfilingService>(typeof(IProfilingService));
        services.AddEngineService<ObjectTypeManager>(typeof(IObjectTypeManager));
        services.AddEngineService<StringInterner>();
        services.AddSingleton<IEntityRegistry, EntityRegistry>();
        services.AddSingleton<IObjectFactory, ObjectFactory>();
        services.AddSingleton<IArenaAllocator, ArenaProxy>();
        return services;
    }
    public static IServiceCollection AddEcsServices(this IServiceCollection services)
    {
        services.AddEngineService<ComponentRegistryService>();
        services.AddEngineService<SharedPool<GameObject>>(sp => new SharedPool<GameObject>(() => new GameObject()), typeof(IObjectPool<GameObject>), typeof(IShrinkable));
        services.AddEngineService<SharedPool<EntityCommandBuffer>>(sp => new SharedPool<EntityCommandBuffer>(() => new EntityCommandBuffer(sp.GetRequiredService<IObjectFactory>(), sp.GetRequiredService<IComponentManager>(), sp.GetRequiredService<IJobSystem>())), typeof(IObjectPool<EntityCommandBuffer>), typeof(IShrinkable));
        services.AddSingleton<ISystemRegistry, SystemRegistry>();
        services.AddSingleton<ISystemExecutionPlanner, SystemExecutionPlanner>();
        services.AddEngineService<SystemManager>(typeof(ISystemManager));
        services.AddEngineService<ArchetypeManager>(typeof(IArchetypeManager));
        services.AddEngineService<ComponentManager>(typeof(IComponentManager));
        services.AddEngineService<ComponentQueryService>(typeof(IComponentQueryService));
        services.AddEngineService<ComponentMessageBus>(typeof(IComponentMessageBus));
        services.AddSystem<Systems.StateCommitSystem>();
        services.AddTransient<IEntityCommandBuffer, EntityCommandBuffer>();
        return services;
    }
    public static IServiceCollection AddNetworkingServices(this IServiceCollection services)
    {
        services.AddEngineService<PluginManager>(typeof(IPluginManager));
        services.AddSingleton<ICommandHistoryService, CommandHistoryService>();
        services.AddSingleton<ICommandMiddleware, LoggingCommandMiddleware>();
        services.AddSingleton<ICommandMiddleware, DiagnosticCommandMiddleware>();
        services.AddEngineService<CommandDispatcher>(typeof(ICommandDispatcher));
        services.AddSingleton<LoggingPacketMiddleware>();
        services.AddSingleton<IPacketDispatcher>(sp =>
        {
            var dispatcher = new PacketDispatcher(sp.GetRequiredService<IJobSystem>());
            dispatcher.AddMiddleware(sp.GetRequiredService<LoggingPacketMiddleware>());
            return dispatcher;
        });
        services.AddSingleton<ISnapshotProvider, SnapshotProvider>();
        services.AddSingleton<ISnapshotSerializer, BitPackedSnapshotSerializer>();
        services.AddSingleton<ISnapshotManager, SnapshotManager>();
        services.AddSingleton<IStateInterpolator, InterpolationService>();
        services.AddEngineService<BinarySnapshotService>();
        services.AddEngineService<SpatialGrid>();
        services.AddEngineService<InterestManager>(typeof(IInterestManager));
        return services;
    }
    public static IServiceCollection AddJobSystem(this IServiceCollection services)
    {
        services.AddSingleton<IComputeService, ComputeService>();
        services.AddEngineService<JobSystem>(typeof(IJobSystem));
        services.AddEngineService<VfsManager>(typeof(IVfsManager));
        services.AddEngineService<ResourceSystem>(typeof(IResourceSystem));
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
        if (module is IShrinkable shrinkable) services.AddSingleton<IShrinkable>(shrinkable);
        if (module is ITickable tickable) services.AddSingleton<ITickable>(tickable);
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
                if (module is IShrinkable shrinkable) services.AddSingleton<IShrinkable>(shrinkable);
                if (module is ITickable tickable) services.AddSingleton<ITickable>(tickable);
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
