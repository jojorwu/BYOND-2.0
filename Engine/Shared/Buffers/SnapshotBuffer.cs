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
    private struct SlabEntry
    {
        public BufferSlab Slab;
        public int BaseOffset;
    }

    private readonly List<SlabEntry> _slabs = new();
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
        _slabs.Add(new SlabEntry { Slab = firstSlab, BaseOffset = 0 });
        _currentSlabIndex = 0;
        _totalCapacity = defaultSize;
    }

    /// <inheritdoc />
    public int Capacity => _totalCapacity;

    /// <inheritdoc />
    public int Position => _slabs[_currentSlabIndex].BaseOffset + _slabs[_currentSlabIndex].Slab.Offset;

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
            int baseOffset = _totalCapacity;
            _slabs.Add(new SlabEntry { Slab = giantSlab, BaseOffset = baseOffset });
            _totalCapacity += length;

            _currentSlabIndex = _slabs.Count - 1;
            giantSlab.Offset = length;
            segmentOffset = baseOffset;
            return new Span<byte>(giantSlab.Ptr, length);
        }

        var entry = _slabs[_currentSlabIndex];
        if (entry.Slab.Offset + length > entry.Slab.Capacity)
        {
            _currentSlabIndex++;
            if (_currentSlabIndex == _slabs.Count)
            {
                var newSlab = new BufferSlab(_defaultSlabSize, fromPool: true, pinned: true);
                _slabs.Add(new SlabEntry { Slab = newSlab, BaseOffset = _totalCapacity });
                _totalCapacity += _defaultSlabSize;
            }

            entry = _slabs[_currentSlabIndex];
            entry.Slab.Offset = 0;
        }

        segmentOffset = entry.BaseOffset + entry.Slab.Offset;
        var span = new Span<byte>(entry.Slab.Ptr + entry.Slab.Offset, length);
        entry.Slab.Offset += length;
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
            var entry = _slabs[index];
            return entry.Slab.Data.AsSpan(offset - entry.BaseOffset, length);
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
            var entry = _slabs[index];
            return entry.Slab.Data.AsMemory(offset - entry.BaseOffset, length);
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
            var entry = _slabs[mid];
            if (offset >= entry.BaseOffset && offset < entry.BaseOffset + entry.Slab.Capacity)
                return mid;
            if (offset < entry.BaseOffset)
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
            for (int i = 64; i < _slabs.Count; i++) _slabs[i].Slab.Dispose();
            _slabs.RemoveRange(64, _slabs.Count - 64);
        }

        // Handle oversized and clear offsets
        for (int i = _slabs.Count - 1; i >= 0; i--)
        {
            if (_slabs[i].Slab.IsOversized)
            {
                _slabs[i].Slab.Dispose();
                _slabs.RemoveAt(i);
            }
            else
            {
                _slabs[i].Slab.Offset = 0;
            }
        }

        // Re-calculate base offsets and total capacity
        _totalCapacity = 0;
        for (int i = 0; i < _slabs.Count; i++)
        {
            var entry = _slabs[i];
            entry.BaseOffset = _totalCapacity;
            _totalCapacity += entry.Slab.Capacity;
            _slabs[i] = entry;
        }

        if (_slabs.Count == 0)
        {
            var firstSlab = new BufferSlab(_defaultSlabSize, fromPool: true, pinned: true);
            _slabs.Add(new SlabEntry { Slab = firstSlab, BaseOffset = 0 });
            _totalCapacity = _defaultSlabSize;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var entry in _slabs) entry.Slab.Dispose();
        _slabs.Clear();
    }
}
