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
        public readonly bool IsOversized;

        public unsafe Slab(int size, bool isOversized = false)
        {
            Capacity = size;
            IsOversized = isOversized;
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
    public int Position => CalculateGlobalOffset(_currentSlabIndex, _offset);

    private int CalculateGlobalOffset(int slabIdx, int localOffset)
    {
        int global = 0;
        for (int i = 0; i < slabIdx; i++) global += _slabs[i].Capacity;
        return global + localOffset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Span<byte> AcquireSegment(int length, out int segmentOffset)
    {
        if (length > _defaultSlabSize)
        {
            // Support oversized segments by creating a dedicated slab
            var giantSlab = new Slab(length, isOversized: true);

            // Insert it before the current slab if possible, or just append it and move there
            _slabs.Add(giantSlab);
            _currentSlabIndex = _slabs.Count - 1;
            _offset = length;
            segmentOffset = CalculateGlobalOffset(_currentSlabIndex, 0);
            return new Span<byte>(giantSlab.Ptr, length);
        }

        if (_offset + length > _slabs[_currentSlabIndex].Capacity)
        {
            _currentSlabIndex++;
            if (_currentSlabIndex == _slabs.Count)
            {
                _slabs.Add(new Slab(_defaultSlabSize));
            }
            else if (_slabs[_currentSlabIndex].IsOversized)
            {
                // Skip oversized slabs when growing normally
                _slabs.Add(new Slab(_defaultSlabSize));
                _currentSlabIndex = _slabs.Count - 1;
            }
            _offset = 0;
        }

        segmentOffset = CalculateGlobalOffset(_currentSlabIndex, _offset);
        var slab = _slabs[_currentSlabIndex];
        var span = new Span<byte>(slab.Ptr + _offset, length);
        _offset += length;
        return span;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetSegmentAsSpan(int offset, int length)
    {
        int global = 0;
        for (int i = 0; i < _slabs.Count; i++)
        {
            if (offset < global + _slabs[i].Capacity)
            {
                return _slabs[i].Data.AsSpan(offset - global, length);
            }
            global += _slabs[i].Capacity;
        }
        throw new ArgumentOutOfRangeException(nameof(offset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<byte> GetSegmentAsMemory(int offset, int length)
    {
        int global = 0;
        for (int i = 0; i < _slabs.Count; i++)
        {
            if (offset < global + _slabs[i].Capacity)
            {
                return _slabs[i].Data.AsMemory(offset - global, length);
            }
            global += _slabs[i].Capacity;
        }
        throw new ArgumentOutOfRangeException(nameof(offset));
    }

    public void Reset()
    {
        _currentSlabIndex = 0;
        _offset = 0;
        // Optimization: Keep up to 4 normal slabs, prune the rest including all oversized ones
        for (int i = _slabs.Count - 1; i >= 0; i--)
        {
            if (_slabs[i].IsOversized || i >= 4)
            {
                _slabs[i].Dispose();
                _slabs.RemoveAt(i);
            }
        }
        if (_slabs.Count == 0) _slabs.Add(new Slab(_defaultSlabSize));
    }

    public void Dispose()
    {
        foreach (var slab in _slabs) slab.Dispose();
        _slabs.Clear();
    }
}
