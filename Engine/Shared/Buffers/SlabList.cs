using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Shared.Buffers;

/// <summary>
/// Internal helper for managing a list of memory slabs with base offset tracking.
/// Provides common functionality for <see cref="SnapshotBuffer"/> and <see cref="ArenaAllocator"/>.
/// </summary>
internal struct SlabList
{
    /// <summary>
    /// Represents an entry in the slab list.
    /// </summary>
    public struct Entry
    {
        /// <summary>
        /// The memory slab.
        /// </summary>
        public BufferSlab Slab;

        /// <summary>
        /// The optional reference counting wrapper.
        /// </summary>
        public RefCountedBufferSlab? RefCountedSlab;

        /// <summary>
        /// The global base offset of this slab within the buffer.
        /// </summary>
        public long BaseOffset;
    }

    private readonly List<Entry> _entries;

    /// <summary>
    /// Initializes a new instance of the <see cref="SlabList"/> struct.
    /// </summary>
    public SlabList()
    {
        _entries = new List<Entry>();
    }

    /// <summary>
    /// Gets the number of slabs in the list.
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Gets or sets the entry at the specified index.
    /// </summary>
    public Entry this[int index]
    {
        get => _entries[index];
        set => _entries[index] = value;
    }

    /// <summary>
    /// Adds a slab to the end of the list.
    /// </summary>
    public void Add(BufferSlab slab, long baseOffset)
    {
        _entries.Add(new Entry { Slab = slab, BaseOffset = baseOffset });
    }

    /// <summary>
    /// Adds a reference-counted slab to the end of the list.
    /// </summary>
    public void Add(RefCountedBufferSlab slab, long baseOffset)
    {
        _entries.Add(new Entry { Slab = slab.Slab, RefCountedSlab = slab, BaseOffset = baseOffset });
    }

    /// <summary>
    /// Inserts a slab at the specified index and updates subsequent offsets.
    /// </summary>
    public void Insert(int index, BufferSlab slab, long baseOffset)
    {
        _entries.Insert(index, new Entry { Slab = slab, BaseOffset = baseOffset });

        // Update subsequent base offsets to maintain consistency
        long currentBase = baseOffset + slab.Capacity;
        for (int i = index + 1; i < _entries.Count; i++)
        {
            var e = _entries[i];
            e.BaseOffset = currentBase;
            _entries[i] = e;
            currentBase += e.Slab.Capacity;
        }
    }

    /// <summary>
    /// Removes the slab at the specified index.
    /// </summary>
    public void RemoveAt(int index)
    {
        _entries.RemoveAt(index);
    }

    /// <summary>
    /// Removes a range of slabs.
    /// </summary>
    public void RemoveRange(int index, int count)
    {
        _entries.RemoveRange(index, count);
    }

    /// <summary>
    /// Clears the list.
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
    }

    /// <summary>
    /// Prunes the list to the specified maximum count, returning extra slabs to the allocator.
    /// </summary>
    public void Prune(int maxCount, ISlabAllocator allocator)
    {
        if (_entries.Count > maxCount)
        {
            for (int i = maxCount; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.RefCountedSlab != null) entry.RefCountedSlab.Dispose();
                else allocator.Return(entry.Slab);
            }
            _entries.RemoveRange(maxCount, _entries.Count - maxCount);
        }
    }

    /// <summary>
    /// Returns the entries as a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    public ReadOnlySpan<Entry> AsSpan() => CollectionsMarshal.AsSpan(_entries);

    /// <summary>
    /// Performs a binary search to find the index of the slab containing the specified global offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FindSlabIndex(long offset)
    {
        var spans = AsSpan();
        int low = 0;
        int high = spans.Length - 1;
        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            ref readonly var entry = ref spans[mid];
            if (offset >= entry.BaseOffset && offset < entry.BaseOffset + entry.Slab.Capacity)
                return mid;
            if (offset < entry.BaseOffset)
                high = mid - 1;
            else
                low = mid + 1;
        }
        return -1;
    }

    /// <summary>
    /// Resolves a global offset and length to a span within a slab.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSegmentAsSpan(long offset, int length)
    {
        int index = FindSlabIndex(offset);
        if (index >= 0)
        {
            ref readonly var entry = ref CollectionsMarshal.AsSpan(_entries)[index];
            if (offset + length > entry.BaseOffset + entry.Slab.Capacity)
                throw new ArgumentException("Segment spans across multiple slabs.", nameof(length));
            return entry.Slab.Data.AsSpan((int)(offset - entry.BaseOffset), length);
        }
        throw new ArgumentOutOfRangeException(nameof(offset), "Offset is outside of the buffer's address space.");
    }

    /// <summary>
    /// Resolves a global offset and length to a memory block within a slab.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<byte> GetSegmentAsMemory(long offset, int length)
    {
        int index = FindSlabIndex(offset);
        if (index >= 0)
        {
            ref readonly var entry = ref CollectionsMarshal.AsSpan(_entries)[index];
            if (offset + length > entry.BaseOffset + entry.Slab.Capacity)
                throw new ArgumentException("Segment spans across multiple slabs.", nameof(length));
            return new ReadOnlyMemory<byte>(entry.Slab.Data, (int)(offset - entry.BaseOffset), length);
        }
        throw new ArgumentOutOfRangeException(nameof(offset), "Offset is outside of the buffer's address space.");
    }

    public List<Entry>.Enumerator GetEnumerator() => _entries.GetEnumerator();
}
