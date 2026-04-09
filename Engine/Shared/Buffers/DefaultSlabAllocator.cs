using System;
using System.Buffers;
using Shared.Attributes;

namespace Shared.Buffers;

/// <summary>
/// Default implementation of <see cref="ISlabAllocator"/> that utilizes <see cref="ArrayPool{T}.Shared"/> for normal slabs.
/// </summary>
[EngineService(typeof(ISlabAllocator))]
public sealed class DefaultSlabAllocator : ISlabAllocator
{
    /// <inheritdoc />
    public BufferSlab Allocate(int size, bool pinned, bool isOversized = false)
    {
        // For normal slabs, we rent from the pool.
        // Oversized slabs bypass the pool to avoid polluting it with non-standard sizes.
        bool fromPool = !isOversized;
        return new BufferSlab(size, fromPool, pinned, isOversized);
    }

    /// <inheritdoc />
    public void Return(BufferSlab slab)
    {
        slab.Dispose();
    }
}
