using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Shared.Interfaces;

namespace Shared.Buffers;

/// <summary>
/// A growable buffer designed for game state snapshot serialization.
/// Utilizes multiple slabs of memory and supports efficient segment retrieval via binary search.
/// </summary>
public sealed class SnapshotBuffer : IBuffer, IBufferWriter<byte>, IDisposable
{
    private SlabList _slabs = new();
    private int _currentSlabIndex;
    private SlabLookupCache _lookupCache = new();
    private readonly int _defaultSlabSize;
    private long _totalCapacity;
    private readonly ISlabAllocator _allocator;
    private readonly IDiagnosticBus? _diagnosticBus;

    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotBuffer"/> class with a default slab size and allocator.
    /// </summary>
    /// <param name="allocator">The allocator used to manage buffer slabs. If null, <see cref="DefaultSlabAllocator"/> is used.</param>
    /// <param name="diagnosticBus">The diagnostic bus to publish metrics to.</param>
    /// <param name="defaultSize">The default size for each slab in bytes. Defaults to 16MB.</param>
    public SnapshotBuffer(ISlabAllocator? allocator = null, IDiagnosticBus? diagnosticBus = null, int defaultSize = 16 * 1024 * 1024)
    {
        _allocator = allocator ?? new DefaultSlabAllocator();
        _diagnosticBus = diagnosticBus;
        _defaultSlabSize = defaultSize;
        var firstSlab = _allocator.Allocate(defaultSize, pinned: true);
        _slabs.Add(firstSlab, 0);
        _currentSlabIndex = 0;
        _totalCapacity = defaultSize;
    }

    /// <inheritdoc />
    public long Capacity => _totalCapacity;

    /// <inheritdoc />
    public long Position => (long)_slabs[_currentSlabIndex].BaseOffset + _slabs[_currentSlabIndex].Slab.Offset;

    /// <inheritdoc />
    public long Length => Position;

    /// <inheritdoc />
    public int SlabCount => _slabs.Count;

    /// <inheritdoc />
    public long TotalAllocatedBytes => _totalCapacity;

    /// <inheritdoc />
    public bool IsPinned => true;

    /// <summary>
    /// Gets the written portion of the buffer as a <see cref="ReadOnlySequence{byte}"/>.
    /// </summary>
    public ReadOnlySequence<byte> WrittenSequence => Slice(0, Position);

    /// <summary>
    /// Acquires a contiguous segment of the specified length within the buffer.
    /// If the current slab cannot accommodate the segment, a new slab is allocated or retrieved.
    /// </summary>
    /// <param name="length">The required segment length in bytes.</param>
    /// <param name="segmentOffset">When this method returns, contains the global offset of the segment.</param>
    /// <returns>A span pointing to the allocated memory segment.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> AcquireSegment(int length, out long segmentOffset)
    {
        var span = GetSpan(length);
        segmentOffset = Position;
        Advance(length);
        return span.Slice(0, length);
    }

    /// <inheritdoc />
    public void Advance(int count)
    {
        if (count == 0) return;
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

        var entry = _slabs[_currentSlabIndex];

        if (entry.Slab.Offset + count > entry.Slab.Capacity)
        {
            throw new InvalidOperationException($"Cannot advance past current slab capacity. Capacity: {entry.Slab.Capacity}, CurrentOffset: {entry.Slab.Offset}, RequestedAdvance: {count}");
        }

        entry.Slab.Offset += count;
        _slabs[_currentSlabIndex] = entry;
    }

    /// <inheritdoc />
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        PrepareSlab(sizeHint);
        var entry = _slabs[_currentSlabIndex];
        return entry.Slab.Data.AsMemory(entry.Slab.Offset, entry.Slab.Capacity - entry.Slab.Offset);
    }

    /// <inheritdoc />
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        PrepareSlab(sizeHint);
        var entry = _slabs[_currentSlabIndex];
        return entry.Slab.Data.AsSpan(entry.Slab.Offset, entry.Slab.Capacity - entry.Slab.Offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PrepareSlab(int sizeHint)
    {
        if (sizeHint < 0) throw new ArgumentOutOfRangeException(nameof(sizeHint), "Size hint must be non-negative.");
        if (sizeHint == 0) sizeHint = 1;

        var currentEntry = _slabs[_currentSlabIndex];

        if (currentEntry.Slab.Offset + sizeHint > currentEntry.Slab.Capacity)
        {
            long nextBaseOffset = currentEntry.BaseOffset + currentEntry.Slab.Capacity;

            if (sizeHint > _defaultSlabSize)
            {
                var giantSlab = _allocator.Allocate(sizeHint, pinned: true, isOversized: true);
                try
                {
                    _currentSlabIndex++;
                    _slabs.Insert(_currentSlabIndex, giantSlab, nextBaseOffset);
                    _totalCapacity += giantSlab.Capacity;
                }
                catch
                {
                    _allocator.Return(giantSlab);
                    throw;
                }
            }
            else
            {
                _currentSlabIndex++;
                if (_currentSlabIndex == _slabs.Count)
                {
                    var newSlab = _allocator.Allocate(_defaultSlabSize, pinned: true);
                    try
                    {
                        _slabs.Add(newSlab, nextBaseOffset);
                        _totalCapacity += newSlab.Capacity;
                    }
                    catch
                    {
                        _allocator.Return(newSlab);
                        throw;
                    }
                }
                else
                {
                    var entry = _slabs[_currentSlabIndex];
                    entry.Slab.Offset = 0;
                    entry.BaseOffset = nextBaseOffset;
                    _slabs[_currentSlabIndex] = entry;
                }
            }
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetSegmentAsSpan(long offset, int length) => GetSegmentInternal(offset, length);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetMutableSegmentAsSpan(long offset, int length) => GetSegmentInternal(offset, length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<byte> GetSegmentInternal(long offset, int length)
    {
        if (_lookupCache.TryResolve(offset, length, _slabs, out Span<byte> result)) return result;

        int index = _slabs.FindSlabIndex(offset);
        if (index >= 0)
        {
            _lookupCache.Update(index);
            return _slabs.GetSegmentAsSpan(offset, length);
        }
        throw new InvalidBufferOffsetException($"Offset {offset} is outside of the buffer's address space.");
    }

    /// <summary>
    /// Returns all written data as a collection of memory segments.
    /// Useful for zero-copy reading using <see cref="BitReader"/>.
    /// </summary>
    /// <returns>A provider that allows allocation-free iteration over written memory segments.</returns>
    public SegmentProvider GetSegments()
    {
        return new SegmentProvider(_slabs, _currentSlabIndex);
    }

    /// <summary>
    /// Returns the slabs used by this buffer as reference-counted objects.
    /// </summary>
    public IReadOnlyList<RefCountedBufferSlab> GetRefCountedSlabs()
    {
        var result = new List<RefCountedBufferSlab>(_slabs.Count);
        for (int i = 0; i < _slabs.Count; i++)
        {
            var entry = _slabs[i];
            if (entry.RefCountedSlab == null)
            {
                entry.RefCountedSlab = new RefCountedBufferSlab(entry.Slab, _allocator);
                _slabs[i] = entry;
            }
            entry.RefCountedSlab.AddRef();
            result.Add(entry.RefCountedSlab);
        }
        return result;
    }

    /// <summary>
    /// Attaches an existing reference-counted slab to the end of the buffer.
    /// </summary>
    /// <param name="slab">The slab to attach.</param>
    public void AttachSlab(RefCountedBufferSlab slab)
    {
        long nextBaseOffset = 0;
        if (_slabs.Count > 0)
        {
            var last = _slabs[_slabs.Count - 1];
            nextBaseOffset = last.BaseOffset + last.Slab.Capacity;
        }

        slab.AddRef();
        _slabs.Add(slab, nextBaseOffset);
        _totalCapacity += slab.Slab.Capacity;

        // Advance current slab index if we were at the end
        if (_currentSlabIndex == _slabs.Count - 2 && _slabs[_currentSlabIndex].Slab.Offset == _slabs[_currentSlabIndex].Slab.Capacity)
        {
             _currentSlabIndex++;
        }
    }

    /// <summary>
    /// Provides allocation-free iteration over memory segments.
    /// </summary>
    public readonly struct SegmentProvider
    {
        private readonly SlabList _slabs;
        private readonly int _maxIndex;

        internal SegmentProvider(SlabList slabs, int maxIndex)
        {
            _slabs = slabs;
            _maxIndex = maxIndex;
        }

        /// <summary>
        /// Gets the total number of segments.
        /// </summary>
        public int Count => _maxIndex + 1;

        /// <summary>
        /// Gets the segment at the specified index.
        /// </summary>
        public ReadOnlyMemory<byte> this[int index]
        {
            get
            {
                if (index < 0 || index > _maxIndex) throw new ArgumentOutOfRangeException(nameof(index));
                var entry = _slabs[index];
                return new ReadOnlyMemory<byte>(entry.Slab.Data, 0, entry.Slab.Offset);
            }
        }

        /// <summary>
        /// Returns an enumerator for the segments.
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(_slabs, _maxIndex);

        /// <summary>
        /// Enumerator for memory segments.
        /// </summary>
        public struct Enumerator
        {
            private readonly SlabList _slabs;
            private readonly int _maxIndex;
            private int _index;

            internal Enumerator(SlabList slabs, int maxIndex)
            {
                _slabs = slabs;
                _maxIndex = maxIndex;
                _index = -1;
            }

            /// <inheritdoc cref="System.Collections.IEnumerator.MoveNext"/>
            public bool MoveNext()
            {
                _index++;
                return _index <= _maxIndex;
            }

            /// <inheritdoc cref="System.Collections.IEnumerator.Current"/>
            public ReadOnlyMemory<byte> Current
            {
                get
                {
                    var entry = _slabs[_index];
                    return new ReadOnlyMemory<byte>(entry.Slab.Data, 0, entry.Slab.Offset);
                }
            }
        }

        /// <summary>
        /// Converts the segments to a temporary array.
        /// Warning: This operation allocates memory.
        /// </summary>
        public ReadOnlyMemory<byte>[] ToArray()
        {
            var result = new ReadOnlyMemory<byte>[Count];
            for (int i = 0; i < Count; i++) result[i] = this[i];
            return result;
        }
    }

    /// <summary>
    /// Returns a <see cref="ReadOnlyMemory{T}"/> over a previously acquired segment.
    /// </summary>
    /// <param name="offset">The global offset of the segment.</param>
    /// <param name="length">The length of the segment.</param>
    /// <returns>A read-only memory block covering the requested segment.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<byte> GetSegmentAsMemory(long offset, int length)
    {
        if (_lookupCache.TryResolve(offset, length, _slabs, out ReadOnlyMemory<byte> result)) return result;

        int index = _slabs.FindSlabIndex(offset);
        if (index >= 0)
        {
            _lookupCache.Update(index);
            return _slabs.GetSegmentAsMemory(offset, length);
        }
        throw new ArgumentOutOfRangeException(nameof(offset), "Offset is outside of the buffer's address space.");
    }

    /// <inheritdoc />
    public void Shrink()
    {
        _slabs.Prune(_currentSlabIndex + 1, _allocator);
        _totalCapacity = 0;
        for (int i = 0; i < _slabs.Count; i++)
        {
            _totalCapacity += _slabs[i].Slab.Capacity;
        }
    }

    /// <inheritdoc />
    public void CopyTo(System.IO.Stream destination)
    {
        var segments = GetSegments();
        foreach (var segment in segments)
        {
            destination.Write(segment.Span);
        }
    }

    /// <inheritdoc />
    public void CopyTo(Span<byte> destination)
    {
        long totalWritten = 0;
        var segments = GetSegments();
        foreach (var segment in segments)
        {
            segment.Span.CopyTo(destination.Slice((int)totalWritten));
            totalWritten += segment.Length;
        }
    }

    /// <summary>
    /// Returns a view of a portion of the buffer as a <see cref="ReadOnlySequence{byte}"/>.
    /// </summary>
    /// <param name="offset">The global offset of the first byte.</param>
    /// <param name="length">The length of the view in bytes.</param>
    /// <returns>A sequence covering the requested range.</returns>
    public ReadOnlySequence<byte> Slice(long offset, long length)
    {
        if (offset < 0 || length < 0 || offset + length > Position)
            throw new ArgumentOutOfRangeException();

        var builder = new SequenceBuilder();
        long remaining = length;
        long currentOffset = offset;

        while (remaining > 0)
        {
            int index = _slabs.FindSlabIndex(currentOffset);
            if (index < 0) break;

            var entry = _slabs[index];
            long slabStart = currentOffset - entry.BaseOffset;
            int take = (int)Math.Min(remaining, entry.Slab.Capacity - slabStart);

            builder.Add(new ReadOnlyMemory<byte>(entry.Slab.Data, (int)slabStart, take));

            remaining -= take;
            currentOffset += take;
        }

        return builder.Build();
    }

    /// <inheritdoc />
    public void Reset()
    {
        _currentSlabIndex = 0;
        _lookupCache.Reset();
        _slabs.Prune(64, _allocator);

        for (int i = _slabs.Count - 1; i >= 0; i--)
        {
            if (_slabs[i].Slab.IsOversized)
            {
                _allocator.Return(_slabs[i].Slab);
                _slabs.RemoveAt(i);
            }
            else
            {
                var entry = _slabs[i];
                entry.Slab.Offset = 0;
                entry.BaseOffset = 0;
                _slabs[i] = entry;
            }
        }

        _totalCapacity = 0;
        long currentBase = 0;
        for (int i = 0; i < _slabs.Count; i++)
        {
            var entry = _slabs[i];
            entry.BaseOffset = currentBase;
            _slabs[i] = entry;
            _totalCapacity += entry.Slab.Capacity;
            currentBase += entry.Slab.Capacity;
        }

        if (_slabs.Count == 0)
        {
            var firstSlab = _allocator.Allocate(_defaultSlabSize, pinned: true);
            _slabs.Add(firstSlab, 0);
            _totalCapacity = _defaultSlabSize;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var entry in _slabs)
        {
            if (entry.RefCountedSlab != null) entry.RefCountedSlab.Dispose();
            else _allocator.Return(entry.Slab);
        }
        _slabs.Clear();
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> GetDiagnosticInfo()
    {
        long pooledBytes = 0;
        long unmanagedBytes = 0;
        foreach (var entry in _slabs)
        {
            if (entry.Slab.IsFromPool) pooledBytes += entry.Slab.Capacity;
            else unmanagedBytes += entry.Slab.Capacity;
        }

        var info = new Dictionary<string, object>
        {
            ["Capacity"] = Capacity,
            ["Position"] = Position,
            ["Length"] = Length,
            ["SlabCount"] = SlabCount,
            ["TotalAllocatedBytes"] = TotalAllocatedBytes,
            ["PooledBytes"] = pooledBytes,
            ["UnmanagedBytes"] = unmanagedBytes,
            ["IsPinned"] = IsPinned
        };

        _diagnosticBus?.Publish("Buffer", "SnapshotBuffer Stats", info, (m, state) =>
        {
            foreach (var kvp in state) m.Add(kvp.Key, kvp.Value.ToString() ?? string.Empty);
        });

        return info;
    }
}
