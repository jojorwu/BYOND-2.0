using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Shared.Buffers;

/// <summary>
/// A growable buffer designed for game state snapshot serialization.
/// Utilizes multiple slabs of memory and supports efficient segment retrieval via binary search.
/// </summary>
public sealed class SnapshotBuffer : IBuffer, IDisposable
{
    private readonly List<BufferSlab> _slabs = new();
    private readonly List<int> _slabBaseOffsets = new();
    private int _currentSlabIndex;
    private readonly int _defaultSlabSize;
    private int _totalCapacity;

    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotBuffer"/> class with a default slab size.
    /// </summary>
    /// <param name="defaultSize">The default size for each slab in bytes. Defaults to 16MB.</param>
    public SnapshotBuffer(int defaultSize = 16 * 1024 * 1024)
    {
        _defaultSlabSize = defaultSize;
        var firstSlab = new BufferSlab(defaultSize, fromPool: true, pinned: true);
        _slabs.Add(firstSlab);
        _slabBaseOffsets.Add(0);
        _currentSlabIndex = 0;
        _totalCapacity = defaultSize;
    }

    /// <inheritdoc />
    public int Capacity => _totalCapacity;

    /// <inheritdoc />
    public int Position => _slabBaseOffsets[_currentSlabIndex] + _slabs[_currentSlabIndex].Offset;

    /// <summary>
    /// Acquires a contiguous segment of the specified length within the buffer.
    /// If the current slab cannot accommodate the segment, a new slab is allocated or retrieved.
    /// </summary>
    /// <param name="length">The required segment length in bytes.</param>
    /// <param name="segmentOffset">When this method returns, contains the global offset of the segment.</param>
    /// <returns>A span pointing to the allocated memory segment.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Span<byte> AcquireSegment(int length, out int segmentOffset)
    {
        if (length > _defaultSlabSize)
        {
            var giantSlab = new BufferSlab(length, fromPool: false, pinned: true, isOversized: true);
            _slabs.Add(giantSlab);
            _slabBaseOffsets.Add(_totalCapacity);
            _totalCapacity += length;

            _currentSlabIndex = _slabs.Count - 1;
            giantSlab.Offset = length;
            segmentOffset = _slabBaseOffsets[_currentSlabIndex];
            return new Span<byte>(giantSlab.Ptr, length);
        }

        var currentSlab = _slabs[_currentSlabIndex];
        if (currentSlab.Offset + length > currentSlab.Capacity)
        {
            _currentSlabIndex++;
            if (_currentSlabIndex == _slabs.Count)
            {
                var newSlab = new BufferSlab(_defaultSlabSize, fromPool: true, pinned: true);
                _slabs.Add(newSlab);
                _slabBaseOffsets.Add(_totalCapacity);
                _totalCapacity += _defaultSlabSize;
            }

            currentSlab = _slabs[_currentSlabIndex];
            currentSlab.Offset = 0;
        }

        segmentOffset = _slabBaseOffsets[_currentSlabIndex] + currentSlab.Offset;
        var span = new Span<byte>(currentSlab.Ptr + currentSlab.Offset, length);
        currentSlab.Offset += length;
        return span;
    }

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{T}"/> over a previously acquired segment.
    /// </summary>
    /// <param name="offset">The global offset of the segment.</param>
    /// <param name="length">The length of the segment.</param>
    /// <returns>A read-only span covering the requested segment.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetSegmentAsSpan(int offset, int length)
    {
        // Binary search for the slab containing the offset
        int index = FindSlabIndex(offset);
        if (index >= 0)
        {
            return _slabs[index].Data.AsSpan(offset - _slabBaseOffsets[index], length);
        }
        throw new ArgumentOutOfRangeException(nameof(offset));
    }

    /// <summary>
    /// Returns a <see cref="ReadOnlyMemory{T}"/> over a previously acquired segment.
    /// </summary>
    /// <param name="offset">The global offset of the segment.</param>
    /// <param name="length">The length of the segment.</param>
    /// <returns>A read-only memory block covering the requested segment.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<byte> GetSegmentAsMemory(int offset, int length)
    {
        int index = FindSlabIndex(offset);
        if (index >= 0)
        {
            return _slabs[index].Data.AsMemory(offset - _slabBaseOffsets[index], length);
        }
        throw new ArgumentOutOfRangeException(nameof(offset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindSlabIndex(int offset)
    {
        int low = 0;
        int high = _slabs.Count - 1;
        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            int baseOffset = _slabBaseOffsets[mid];
            if (offset >= baseOffset && offset < baseOffset + _slabs[mid].Capacity)
                return mid;
            if (offset < baseOffset)
                high = mid - 1;
            else
                low = mid + 1;
        }
        return -1;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _currentSlabIndex = 0;

        // Prune excessive slabs
        if (_slabs.Count > 64)
        {
            for (int i = 64; i < _slabs.Count; i++) _slabs[i].Dispose();
            _slabs.RemoveRange(64, _slabs.Count - 64);
            _slabBaseOffsets.RemoveRange(64, _slabBaseOffsets.Count - 64);
        }

        // Handle oversized and clear offsets
        for (int i = _slabs.Count - 1; i >= 0; i--)
        {
            if (_slabs[i].IsOversized)
            {
                _slabs[i].Dispose();
                _slabs.RemoveAt(i);
                _slabBaseOffsets.RemoveAt(i);
            }
            else
            {
                _slabs[i].Offset = 0;
            }
        }

        // Re-calculate base offsets and total capacity
        _totalCapacity = 0;
        for (int i = 0; i < _slabs.Count; i++)
        {
            _slabBaseOffsets[i] = _totalCapacity;
            _totalCapacity += _slabs[i].Capacity;
        }

        if (_slabs.Count == 0)
        {
            var firstSlab = new BufferSlab(_defaultSlabSize, fromPool: true, pinned: true);
            _slabs.Add(firstSlab);
            _slabBaseOffsets.Add(0);
            _totalCapacity = _defaultSlabSize;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var slab in _slabs) slab.Dispose();
        _slabs.Clear();
    }
}
