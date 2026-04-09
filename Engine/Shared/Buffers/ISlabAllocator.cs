using System;

namespace Shared.Buffers;

/// <summary>
/// Defines a contract for allocating and reclaiming <see cref="BufferSlab"/> instances.
/// Allows for different allocation strategies (e.g., pooled vs. dedicated memory).
/// </summary>
public interface ISlabAllocator
{
    /// <summary>
    /// Allocates a new <see cref="BufferSlab"/> of the specified size.
    /// </summary>
    /// <param name="size">The required capacity in bytes.</param>
    /// <param name="pinned">If true, the memory should be pinned in the GC heap.</param>
    /// <param name="isOversized">If true, indicates that the slab is for an oversized request and might bypass pooling.</param>
    /// <returns>A new <see cref="BufferSlab"/> instance.</returns>
    BufferSlab Allocate(int size, bool pinned, bool isOversized = false);

    /// <summary>
    /// Returns a slab to the allocator for potential reuse or disposal.
    /// </summary>
    /// <param name="slab">The slab to return.</param>
    void Return(BufferSlab slab);
}
