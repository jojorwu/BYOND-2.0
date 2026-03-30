using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Shared.Utils;

/// <summary>
/// A high-performance, semi-persistent buffer for snapshot segments.
/// Minimizes GC pressure by using a large pre-allocated slab and manual offset management.
/// Designed for a single-producer (the Snapshot service) during a single tick.
/// </summary>
public sealed class SnapshotBuffer : IDisposable
{
    private byte[] _slab;
    private int _offset;
    private readonly int _slabSize;
    private readonly GCHandle _handle;
    private readonly unsafe byte* _ptr;

    public unsafe SnapshotBuffer(int size = 16 * 1024 * 1024) // 16MB default slab
    {
        _slabSize = size;
        _slab = GC.AllocateUninitializedArray<byte>(size, pinned: true);
        _handle = GCHandle.Alloc(_slab, GCHandleType.Pinned);
        _ptr = (byte*)_handle.AddrOfPinnedObject();
        _offset = 0;
    }

    public unsafe byte* Pointer => _ptr;
    public int Capacity => _slabSize;
    public int Position => _offset;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Span<byte> AcquireSegment(int length, out int segmentOffset)
    {
        if (_offset + length > _slabSize)
        {
            throw new InvalidOperationException("Snapshot slab exhausted. Increase slab size or clear more frequently.");
        }

        segmentOffset = _offset;
        var span = new Span<byte>(_ptr + _offset, length);
        _offset += length;
        return span;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<byte> GetSegmentAsMemory(int offset, int length)
    {
        return _slab.AsMemory(offset, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetSegmentAsSpan(int offset, int length)
    {
        return _slab.AsSpan(offset, length);
    }

    public void Reset()
    {
        _offset = 0;
        // We don't necessarily need to clear the slab as we always write new data
    }

    public void Dispose()
    {
        if (_handle.IsAllocated)
        {
            _handle.Free();
        }
        _slab = null!;
    }
}
