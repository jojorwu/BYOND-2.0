using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Shared.Services;

/// <summary>
/// Assigns a unique, stable index to each component type for bitmask operations.
/// </summary>
public static class ComponentIdRegistry
{
    private static int _nextId = 0;
    private static readonly ConcurrentDictionary<Type, int> _typeToId = new();

    public static int GetId<T>() where T : class, Interfaces.IComponent => GetId(typeof(T));

    public static int GetId(Type type)
    {
        return _typeToId.GetOrAdd(type, _ => Interlocked.Increment(ref _nextId) - 1);
    }

    public static int Count => _nextId;
}
