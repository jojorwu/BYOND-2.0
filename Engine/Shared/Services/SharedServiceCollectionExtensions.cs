using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Shared.Interfaces;
using Shared.Messaging;
using Shared.Services;
using Shared.Attributes;

namespace Shared.Services;
public static class SharedServiceCollectionExtensions
{
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

    public static IServiceCollection AddAutoRegisteredServices(this IServiceCollection services, Assembly assembly)
    {
        var serviceTypes = assembly.GetTypes()
            .Select(t => new { Type = t, Attribute = t.GetCustomAttribute<EngineServiceAttribute>() })
            .Where(x => x.Attribute != null);

        foreach (var x in serviceTypes)
        {
            var method = typeof(SharedServiceCollectionExtensions)
                .GetMethods()
                .First(m => m.Name == nameof(AddEngineService) && m.IsGenericMethod && m.GetParameters().Length == 2);

            var generic = method.MakeGenericMethod(x.Type);
            generic.Invoke(null, new object[] { services, x.Attribute!.Interfaces });
        }
        return services;
    }

    public static IServiceCollection AddSharedEngineServices(this IServiceCollection services)
    {
        services.AddAutoRegisteredServices(typeof(SharedServiceCollectionExtensions).Assembly);

        services.AddEngineService<Shared.Config.ConfigurationManager>(typeof(Shared.Config.IConfigurationManager));
        services.AddEngineService<Shared.Config.ConsoleCommandManager>(typeof(Shared.Config.IConsoleCommandManager));
        // Manual registrations below are now redundant if they were marked with [EngineService]
        // But we keep some groups for organization or specialized factory logic.
        services.AddEcsServices();
        services.AddNetworkingServices();
        services.AddJobSystem();
        return services;
    }
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IArenaAllocator, ArenaAllocator>();
        services.AddSingleton<ILifecycleOrchestrator, DefaultLifecycleOrchestrator>();
        return services;
    }
    public static IServiceCollection AddEcsServices(this IServiceCollection services)
    {
        services.AddEngineService<SharedPool<GameObject>>(sp => new SharedPool<GameObject>(() => new GameObject()), typeof(IObjectPool<GameObject>), typeof(IShrinkable));
        services.AddEngineService<SharedPool<EntityCommandBuffer>>(sp => new SharedPool<EntityCommandBuffer>(() => new EntityCommandBuffer(sp.GetRequiredService<IObjectFactory>(), sp.GetRequiredService<IComponentManager>(), sp.GetRequiredService<IJobSystem>())), typeof(IObjectPool<EntityCommandBuffer>), typeof(IShrinkable));
        services.AddSystem<Systems.StateCommitSystem>();
        services.AddTransient<IEntityCommandBuffer, EntityCommandBuffer>();
        return services;
    }
    public static IServiceCollection AddNetworkingServices(this IServiceCollection services)
    {
        services.AddSingleton<ICommandMiddleware, LoggingCommandMiddleware>();
        services.AddSingleton<ICommandMiddleware, DiagnosticCommandMiddleware>();
        services.AddSingleton<LoggingPacketMiddleware>();
        services.AddSingleton<IPacketDispatcher>(sp =>
        {
            var dispatcher = new PacketDispatcher(sp.GetRequiredService<IJobSystem>(), sp);
            dispatcher.AddMiddleware(sp.GetRequiredService<LoggingPacketMiddleware>());
            return dispatcher;
        });

        services.AddSingleton<IPacketHandler, Shared.Networking.Handlers.SnapshotHandler>();
        services.AddSingleton<IPacketHandler, Shared.Networking.Handlers.NetworkMessageHandler>();

        services.AddSingleton<Shared.Networking.Handlers.IMessageHandler, Shared.Networking.Handlers.SoundMessageHandler>();
        services.AddSingleton<Shared.Networking.Handlers.IMessageHandler, Shared.Networking.Handlers.StopSoundMessageHandler>();
        services.AddSingleton<Shared.Networking.Handlers.IMessageHandler, Shared.Networking.Handlers.CVarSyncMessageHandler>();
        services.AddSingleton<Shared.Networking.Handlers.IMessageHandler, Shared.Networking.Handlers.ClientCommandMessageHandler>();
        services.AddSingleton<Shared.Networking.Handlers.IMessageHandler, Shared.Networking.Handlers.ClientInputMessageHandler>();

        services.AddSingleton<INetworkFieldHandler, Shared.Networking.FieldHandlers.TypeFieldHandler>();
        services.AddSingleton<INetworkFieldHandler, Shared.Networking.FieldHandlers.TransformFieldHandler>();
        services.AddSingleton<INetworkFieldHandler, Shared.Networking.FieldHandlers.VisualFieldHandler>();
        services.AddSingleton<INetworkFieldHandler, Shared.Networking.FieldHandlers.VariablesFieldHandler>();
        services.AddSingleton<INetworkFieldHandler, Shared.Networking.FieldHandlers.ComponentsFieldHandler>();
        return services;
    }
    public static IServiceCollection AddJobSystem(this IServiceCollection services)
    {
        return services;
    }
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
    public static IServiceCollection AddEngineModule<T>(this IServiceCollection services) where T : class, IEngineModule, new()
    {
        var module = new T();
        services.AddSingleton<IEngineModule>(module);
        if (module is IShrinkable shrinkable) services.AddSingleton<IShrinkable>(shrinkable);
        if (module is ITickable tickable) services.AddSingleton<ITickable>(tickable);
        module.RegisterServices(services);
        return services;
    }
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
    public static IServiceCollection AddSystem<T>(this IServiceCollection services) where T : class, ISystem
    {
        services.AddSingleton<ISystem, T>();
        return services;
    }
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
