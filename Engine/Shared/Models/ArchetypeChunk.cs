using Shared.Interfaces;
using System;

namespace Shared.Models;

/// <summary>
/// A raw, non-generic view of an archetype's component data for batch processing.
/// </summary>
public readonly struct ArchetypeChunk
{
    public readonly Archetype Archetype;
    public readonly int Start;
    public readonly int Count;
    public readonly long[] EntityIds;

    public ArchetypeChunk(Archetype archetype, int start, int count, long[] entityIds)
    {
        Archetype = archetype;
        Start = start;
        Count = count;
        EntityIds = entityIds;
    }

    /// <summary>
    /// Gets the raw component array for a specific component type.
    /// </summary>
    public T[] GetComponents<T>() where T : class, IComponent
    {
        return Archetype.GetComponentsInternal<T>();
    }
}
