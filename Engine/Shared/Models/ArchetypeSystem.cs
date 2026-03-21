using System;
using System.Collections.Generic;
using Shared.Attributes;
using Shared.Interfaces;

namespace Shared.Models;

/// <summary>
/// Base class for systems that operate on entire chunks of components at once.
/// This maximizes cache locality by processing contiguous arrays.
/// </summary>
public abstract class ArchetypeChunkSystem<T1> : BaseSystem where T1 : class, IComponent
{
    [Query]
    protected EntityQuery<T1> _query = null!;

    public override void Initialize()
    {
        base.Initialize();
        // The SystemManager will inject the query via reflection, but we ensure it's here for safety.
    }

    public override void Tick(IEntityCommandBuffer ecb)
    {
        foreach (var archetype in _query.GetMatchingArchetypes())
        {
            foreach (var chunk in archetype.GetChunks<T1>())
            {
                Tick(chunk, ecb);
            }
        }
    }

    protected abstract void Tick(ArchetypeChunk<T1> chunk, IEntityCommandBuffer ecb);
}

/// <summary>
/// Base class for systems that operate on entire chunks of components (2-component queries).
/// </summary>
public abstract class ArchetypeChunkSystem<T1, T2> : BaseSystem
    where T1 : class, IComponent
    where T2 : class, IComponent
{
    [Query]
    protected EntityQuery<T1, T2> _query = null!;

    public override void Tick(IEntityCommandBuffer ecb)
    {
        foreach (var archetype in _query.GetMatchingArchetypes())
        {
            // For multi-component chunks, we use a simple zip approach for now.
            // In a more advanced implementation, we'd ensure chunk boundaries align.
            var chunks1 = archetype.GetChunks<T1>().GetEnumerator();
            var chunks2 = archetype.GetChunks<T2>().GetEnumerator();

            while (chunks1.MoveNext() && chunks2.MoveNext())
            {
                Tick(chunks1.Current, chunks2.Current, ecb);
            }
        }
    }

    protected abstract void Tick(ArchetypeChunk<T1> chunk1, ArchetypeChunk<T2> chunk2, IEntityCommandBuffer ecb);
}
