using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Services;

/// <summary>
/// A managed engine service that wraps ComponentIdRegistry to participate in the engine lifecycle.
/// </summary>
public class ComponentRegistryService : EngineService, IFreezable
{
    public override string Name => "ComponentRegistry";
    public override int Priority => 1000; // High priority, start early
    public override bool IsCritical => true;

    public override Task InitializeAsync()
    {
        // Initial discovery could happen here, or be deferred to PostInitialize
        return base.InitializeAsync();
    }

    public void Freeze()
    {
        ComponentIdRegistry.Freeze();
    }

    public void RegisterAll(Assembly assembly)
    {
        ComponentIdRegistry.RegisterAll(assembly);
    }

    public int GetId(Type type)
    {
        return ComponentIdRegistry.GetId(type);
    }

    public int GetId<T>() where T : class, IComponent
    {
        return ComponentIdRegistry.GetId<T>();
    }

    public int Count => ComponentIdRegistry.Count;

    public IEnumerable<Type> RegisteredTypes => ComponentIdRegistry.RegisteredTypes;
}
