using System;
using System.Threading;

namespace Shared.Buffers;

/// <summary>
/// A wrapper for <see cref="BufferSlab"/> that adds reference counting for shared ownership.
/// </summary>
public sealed class RefCountedBufferSlab : IDisposable
{
    private readonly BufferSlab _slab;
    private readonly ISlabAllocator _allocator;
    private int _refCount;

    public BufferSlab Slab => _slab;

    internal RefCountedBufferSlab(BufferSlab slab, ISlabAllocator allocator)
    {
        _slab = slab;
        _allocator = allocator;
        _refCount = 1;
    }

    /// <summary>
    /// Increments the reference count.
    /// </summary>
    public void AddRef()
    {
        Interlocked.Increment(ref _refCount);
    }

    /// <summary>
    /// Decrements the reference count and returns the slab to the allocator if it reaches zero.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Decrement(ref _refCount) == 0)
        {
            _allocator.Return(_slab);
        }
    }
}
