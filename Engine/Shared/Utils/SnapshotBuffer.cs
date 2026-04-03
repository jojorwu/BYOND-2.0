using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Shared.Utils;

/// <summary>
/// A high-performance, growable buffer for snapshot segments.
/// Minimizes GC pressure by using pre-allocated slabs and manual offset management.
/// Supports multi-slab architecture to handle extreme loads without overflow.
/// </summary>
public sealed class SnapshotBuffer : IDisposable
{
    private class Slab : IDisposable
    {
        public readonly byte[] Data;
        public readonly GCHandle Handle;
        public readonly unsafe byte* Ptr;
        public readonly int Capacity;

        public unsafe Slab(int size)
        {
            Capacity = size;
            Data = GC.AllocateUninitializedArray<byte>(size, pinned: true);
            Handle = GCHandle.Alloc(Data, GCHandleType.Pinned);
            Ptr = (byte*)Handle.AddrOfPinnedObject();
        }

        public void Dispose()
        {
            if (Handle.IsAllocated) Handle.Free();
        }
    }

    private readonly List<Slab> _slabs = new();
    private int _currentSlabIndex;
    private int _offset;
    private readonly int _defaultSlabSize;

    public SnapshotBuffer(int defaultSize = 16 * 1024 * 1024)
    {
        _defaultSlabSize = defaultSize;
        _slabs.Add(new Slab(defaultSize));
        _currentSlabIndex = 0;
        _offset = 0;
    }

    public int Capacity => _slabs.Sum(s => s.Capacity);
    public int Position => (_currentSlabIndex * _defaultSlabSize) + _offset;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Span<byte> AcquireSegment(int length, out int segmentOffset)
    {
        if (length > _defaultSlabSize)
        {
            throw new ArgumentOutOfRangeException(nameof(length), $"Segment too large ({length}) for slab size ({_defaultSlabSize}).");
        }

        if (_offset + length > _slabs[_currentSlabIndex].Capacity)
        {
            _currentSlabIndex++;
            if (_currentSlabIndex == _slabs.Count)
            {
                _slabs.Add(new Slab(_defaultSlabSize));
            }
            _offset = 0;
        }

        segmentOffset = (_currentSlabIndex * _defaultSlabSize) + _offset;
        var slab = _slabs[_currentSlabIndex];
        var span = new Span<byte>(slab.Ptr + _offset, length);
        _offset += length;
        return span;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetSegmentAsSpan(int offset, int length)
    {
        int slabIdx = offset / _defaultSlabSize;
        int localOffset = offset % _defaultSlabSize;
        if (slabIdx >= _slabs.Count) throw new ArgumentOutOfRangeException(nameof(offset));
        return _slabs[slabIdx].Data.AsSpan(localOffset, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<byte> GetSegmentAsMemory(int offset, int length)
    {
        int slabIdx = offset / _defaultSlabSize;
        int localOffset = offset % _defaultSlabSize;
        if (slabIdx >= _slabs.Count) throw new ArgumentOutOfRangeException(nameof(offset));
        return _slabs[slabIdx].Data.AsMemory(localOffset, length);
    }

    public void Reset()
    {
        _currentSlabIndex = 0;
        _offset = 0;
        // Optimization: Keep 2 extra slabs, prune the rest
        if (_slabs.Count > 4)
        {
            for (int i = 4; i < _slabs.Count; i++) _slabs[i].Dispose();
            _slabs.RemoveRange(4, _slabs.Count - 4);
        }
    }

    public void Dispose()
    {
        foreach (var slab in _slabs) slab.Dispose();
        _slabs.Clear();
    }
}
