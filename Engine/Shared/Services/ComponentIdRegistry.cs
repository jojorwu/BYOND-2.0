using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Shared.Interfaces;

namespace Shared.Services;

/// <summary>
/// Assigns a unique, stable index to each component type for bitmask operations.
/// </summary>
public static class ComponentIdRegistry
{
    private static int _nextId = 0;
    private static readonly ConcurrentDictionary<Type, int> _typeToId = new();

    public static int GetId<T>() where T : class, IComponent => GetId(typeof(T));

    public static int GetId(Type type)
    {
        return _typeToId.GetOrAdd(type, _ => Interlocked.Increment(ref _nextId) - 1);
    }

    /// <summary>
    /// Pre-registers a component type to ensure it has a stable ID.
    /// </summary>
    public static void Register<T>() where T : class, IComponent => Register(typeof(T));

    /// <summary>
    /// Pre-registers a component type to ensure it has a stable ID.
    /// </summary>
    public static void Register(Type type)
    {
        GetId(type);
    }

    /// <summary>
    /// Registers all component types found in the specified assembly.
    /// </summary>
    public static void RegisterAll(Assembly assembly)
    {
        var componentTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IComponent).IsAssignableFrom(t))
            .OrderBy(t => t.FullName); // Sort by FullName for deterministic IDs if called in same order

        foreach (var type in componentTypes)
        {
            Register(type);
        }
    }

    public static int Count => _nextId;
}
