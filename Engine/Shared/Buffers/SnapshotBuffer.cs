using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Shared.Buffers;

public sealed class SnapshotBuffer : IDisposable
{
    private readonly List<BufferSlab> _slabs = new();
    private int _currentSlabIndex;
    private readonly int _defaultSlabSize;
    private int _globalBaseOffset;

    public SnapshotBuffer(int defaultSize = 16 * 1024 * 1024)
    {
        _defaultSlabSize = defaultSize;
        _slabs.Add(new BufferSlab(defaultSize, fromPool: true, pinned: true));
        _currentSlabIndex = 0;
        _globalBaseOffset = 0;
    }

    public int Capacity => _slabs.Sum(s => s.Capacity);
    public int Position => _globalBaseOffset + _slabs[_currentSlabIndex].Offset;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Span<byte> AcquireSegment(int length, out int segmentOffset)
    {
        if (length > _defaultSlabSize)
        {
            var giantSlab = new BufferSlab(length, fromPool: false, pinned: true, isOversized: true);
            _slabs.Add(giantSlab);
            _currentSlabIndex = _slabs.Count - 1;
            _globalBaseOffset = 0;
            for (int i = 0; i < _currentSlabIndex; i++) _globalBaseOffset += _slabs[i].Capacity;
            giantSlab.Offset = length;
            segmentOffset = _globalBaseOffset;
            return new Span<byte>(giantSlab.Ptr, length);
        }

        var currentSlab = _slabs[_currentSlabIndex];
        if (currentSlab.Offset + length > currentSlab.Capacity)
        {
            _currentSlabIndex++;
            if (_currentSlabIndex == _slabs.Count)
            {
                _slabs.Add(new BufferSlab(_defaultSlabSize, fromPool: true, pinned: true));
            }
            else if (_slabs[_currentSlabIndex].IsOversized)
            {
                _slabs.Add(new BufferSlab(_defaultSlabSize, fromPool: true, pinned: true));
                _currentSlabIndex = _slabs.Count - 1;
            }
            _globalBaseOffset = 0;
            for (int i = 0; i < _currentSlabIndex; i++) _globalBaseOffset += _slabs[i].Capacity;
            currentSlab = _slabs[_currentSlabIndex];
            currentSlab.Offset = 0;
        }

        segmentOffset = _globalBaseOffset + currentSlab.Offset;
        var span = new Span<byte>(currentSlab.Ptr + currentSlab.Offset, length);
        currentSlab.Offset += length;
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
        _globalBaseOffset = 0;
        if (_slabs.Count > 64)
        {
            for (int i = 64; i < _slabs.Count; i++) _slabs[i].Dispose();
            _slabs.RemoveRange(64, _slabs.Count - 64);
        }
        for (int i = _slabs.Count - 1; i >= 0; i--)
        {
            if (_slabs[i].IsOversized)
            {
                _slabs[i].Dispose();
                _slabs.RemoveAt(i);
            }
            else
            {
                _slabs[i].Offset = 0;
            }
        }
        if (_slabs.Count == 0) _slabs.Add(new BufferSlab(_defaultSlabSize, fromPool: true, pinned: true));
    }

    public void Dispose()
    {
        foreach (var slab in _slabs) slab.Dispose();
        _slabs.Clear();
    }
}
