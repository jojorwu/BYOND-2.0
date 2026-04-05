using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Shared.Interfaces;

namespace Shared.Services;

using System.Collections.Frozen;

/// <summary>
/// Assigns a unique, stable index to each component type for bitmask operations.
/// </summary>
public static class ComponentIdRegistry
{
    private static int _nextId = 0;
    private static readonly ConcurrentDictionary<Type, int> _typeToId = new();
    private static volatile FrozenDictionary<Type, int> _frozenTypeToId = FrozenDictionary<Type, int>.Empty;
    private static readonly ConcurrentDictionary<Assembly, bool> _processedAssemblies = new();

    public static int GetId<T>() => GetId(typeof(T));

    public static int GetId(Type type)
    {
        // High-performance hot-path for .NET 10 FrozenDictionary
        if (_frozenTypeToId.TryGetValue(type, out int id)) return id;

        // Fallback for types registered during/after initial freeze
        return _typeToId.GetOrAdd(type, _ => Interlocked.Increment(ref _nextId) - 1);
    }

    /// <summary>
    /// Pre-registers a component type to ensure it has a stable ID.
    /// </summary>
    public static void Register<T>() => Register(typeof(T));

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
        if (!_processedAssemblies.TryAdd(assembly, true)) return;

        var componentTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IComponent).IsAssignableFrom(t))
            .OrderBy(t => t.FullName); // Sort by FullName for deterministic IDs if called in same order

        foreach (var type in componentTypes)
        {
            Register(type);
        }
    }

    public static int Count => _nextId;

    /// <summary>
    /// Gets all registered component types.
    /// </summary>
    public static IEnumerable<Type> RegisteredTypes => _typeToId.Keys;

    /// <summary>
    /// Freezes the current registry to maximize lookup performance in .NET 10.
    /// </summary>
    public static void Freeze()
    {
        _frozenTypeToId = _typeToId.ToFrozenDictionary();
    }
}
