using System;
using System.Runtime.CompilerServices;

namespace Shared.Buffers;

/// <summary>
/// A reusable cache for slab lookups to optimize temporal locality in multi-slab buffers.
/// </summary>
internal struct SlabLookupCache
{
    private int _lastIndex;

    /// <summary>
    /// Attempts to resolve the offset using the cached slab index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryResolve(long offset, int length, SlabList slabs, out Span<byte> result)
    {
        var spans = slabs.AsSpan();
        if (_lastIndex >= 0 && _lastIndex < spans.Length)
        {
            ref readonly var entry = ref spans[_lastIndex];
            if (offset >= entry.BaseOffset && offset < entry.BaseOffset + entry.Slab.Capacity)
            {
                if (offset + length > entry.BaseOffset + entry.Slab.Capacity)
                    throw new ArgumentException("Segment spans across multiple slabs.", nameof(length));

                result = entry.Slab.Data.AsSpan((int)(offset - entry.BaseOffset), length);
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Attempts to resolve the offset using the cached slab index (ReadOnlyMemory version).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryResolve(long offset, int length, SlabList slabs, out ReadOnlyMemory<byte> result)
    {
        var spans = slabs.AsSpan();
        if (_lastIndex >= 0 && _lastIndex < spans.Length)
        {
            ref readonly var entry = ref spans[_lastIndex];
            if (offset >= entry.BaseOffset && offset < entry.BaseOffset + entry.Slab.Capacity)
            {
                if (offset + length > entry.BaseOffset + entry.Slab.Capacity)
                    throw new ArgumentException("Segment spans across multiple slabs.", nameof(length));

                result = new ReadOnlyMemory<byte>(entry.Slab.Data, (int)(offset - entry.BaseOffset), length);
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Updates the cached index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(int index)
    {
        _lastIndex = index;
    }

    /// <summary>
    /// Resets the cache.
    /// </summary>
    public void Reset()
    {
        _lastIndex = -1;
    }
}
