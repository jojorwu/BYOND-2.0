using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace Shared.Buffers;

/// <summary>
/// Represents a contiguous block of memory (slab) used by high-performance buffers.
/// Supports both pooled and pinned memory to minimize GC pressure and enable zero-copy operations.
/// </summary>
public sealed class BufferSlab : IDisposable
{
    private bool _isDisposed;

    /// <summary>
    /// The underlying byte array for this slab.
    /// </summary>
    public readonly byte[] Data;

    /// <summary>
    /// The total capacity of this slab in bytes.
    /// </summary>
    public readonly int Capacity;

    /// <summary>
    /// Indicates whether the memory was rented from <see cref="ArrayPool{T}"/>.
    /// </summary>
    public readonly bool IsFromPool;

    /// <summary>
    /// Indicates whether this slab was created to handle an oversized segment request.
    /// </summary>
    public readonly bool IsOversized;

    /// <summary>
    /// The current write/allocation offset within this slab.
    /// </summary>
    public int Offset;

    private GCHandle _handle;

    /// <summary>
    /// A pointer to the start of the pinned memory, or null if the memory is not pinned.
    /// </summary>
    public readonly unsafe byte* Ptr;

    /// <summary>
    /// Returns a <see cref="Span{T}"/> over the entire slab.
    /// </summary>
    public Span<byte> Span => Data.AsSpan();

    /// <summary>
    /// Returns a <see cref="Memory{T}"/> over the entire slab.
    /// </summary>
    public Memory<byte> Memory => Data.AsMemory();

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferSlab"/> class.
    /// </summary>
    /// <param name="size">The required capacity in bytes.</param>
    /// <param name="fromPool">If true, memory is rented from <see cref="ArrayPool{T}.Shared"/>.</param>
    /// <param name="pinned">If true, the memory is pinned in the GC heap.</param>
    /// <param name="isOversized">If true, indicates an oversized segment.</param>
    public unsafe BufferSlab(int size, bool fromPool, bool pinned, bool isOversized = false)
    {
        Capacity = size;
        IsFromPool = fromPool;
        IsOversized = isOversized;
        Offset = 0;

        if (fromPool)
        {
            Data = ArrayPool<byte>.Shared.Rent(size);
        }
        else
        {
            Data = GC.AllocateUninitializedArray<byte>(size, pinned: pinned);
        }

        if (pinned)
        {
            _handle = GCHandle.Alloc(Data, GCHandleType.Pinned);
            Ptr = (byte*)_handle.AddrOfPinnedObject();
        }
        else
        {
            Ptr = null;
        }
    }

    /// <summary>
    /// Finalizer to ensure resources are released if <see cref="Dispose"/> is not called.
    /// </summary>
    ~BufferSlab()
    {
        Dispose(false);
    }

    /// <summary>
    /// Reclaims resources used by the slab, returning memory to the pool if applicable.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        if (_handle.IsAllocated)
        {
            _handle.Free();
        }

        if (disposing)
        {
            if (IsFromPool)
            {
                ArrayPool<byte>.Shared.Return(Data);
            }
        }

        _isDisposed = true;
    }
}
