using System;
using System.Collections.Generic;

namespace Shared.Buffers;

/// <summary>
/// Internal helper for managing a list of memory slabs with base offset tracking.
/// Provides common functionality for <see cref="SnapshotBuffer"/> and <see cref="ArenaAllocator"/>.
/// </summary>
internal struct SlabList
{
    public struct Entry
    {
        public BufferSlab Slab;
        public int BaseOffset;
    }

    private readonly List<Entry> _entries;

    public SlabList()
    {
        _entries = new List<Entry>();
    }

    public int Count => _entries.Count;
    public Entry this[int index]
    {
        get => _entries[index];
        set => _entries[index] = value;
    }

    public void Add(BufferSlab slab, int baseOffset)
    {
        _entries.Add(new Entry { Slab = slab, BaseOffset = baseOffset });
    }

    public void Insert(int index, BufferSlab slab, int baseOffset)
    {
        _entries.Insert(index, new Entry { Slab = slab, BaseOffset = baseOffset });

        // Update subsequent base offsets to maintain consistency
        int currentBase = baseOffset + slab.Capacity;
        for (int i = index + 1; i < _entries.Count; i++)
        {
            var e = _entries[i];
            e.BaseOffset = currentBase;
            _entries[i] = e;
            currentBase += e.Slab.Capacity;
        }
    }

    public void RemoveAt(int index)
    {
        _entries.RemoveAt(index);
    }

    public void RemoveRange(int index, int count)
    {
        _entries.RemoveRange(index, count);
    }

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

    public List<Entry>.Enumerator GetEnumerator() => _entries.GetEnumerator();
}
