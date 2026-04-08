using System;
using System.Collections.Generic;
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
                allocator.Return(_entries[i].Slab);
            }
            _entries.RemoveRange(maxCount, _entries.Count - maxCount);
        }
    }

    /// <summary>
    /// Returns the entries as a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    public ReadOnlySpan<Entry> AsSpan() => CollectionsMarshal.AsSpan(_entries);

    public List<Entry>.Enumerator GetEnumerator() => _entries.GetEnumerator();
}
