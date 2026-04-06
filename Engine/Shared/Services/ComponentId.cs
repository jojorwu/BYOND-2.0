using Shared.Interfaces;
using System.Runtime.CompilerServices;

namespace Shared.Services;

/// <summary>
/// Provides high-performance, zero-allocation access to component IDs.
/// Utilizes generic static initialization to cache the ID for each component type.
/// </summary>
/// <typeparam name="T">The component type.</typeparam>
public static class ComponentId<T>
{
    /// <summary>
    /// The stable, unique identifier for the component type T.
    /// </summary>
    public static readonly int Value = ComponentIdRegistry.GetId(typeof(T));

    /// <summary>
    /// Gets the ID for the component type T.
    /// This is functionally equivalent to <see cref="Value"/> but can be used in expression trees.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Get() => Value;
}
