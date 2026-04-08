using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Shared.Interfaces;

namespace Shared.Buffers;

/// <summary>
/// A fast arena-based memory allocator that utilizes multiple slabs of memory.
/// Suitable for high-frequency allocations that are all reclaimed at once.
/// </summary>
public class ArenaAllocator : IBuffer, IArenaAllocator, IBufferWriter<byte>, IDisposable
{
    private const int DefaultBlockSize = 1024 * 1024; // 1MB
    private SlabList _blocks = new();
    private int _currentBlockIndex;
    private long _baseOffset;
    private long _totalCapacity;
    private readonly ISlabAllocator _allocator;
    private readonly IDiagnosticBus? _diagnosticBus;

    public ArenaAllocator(ISlabAllocator? allocator = null, IDiagnosticBus? diagnosticBus = null)
    {
        _allocator = allocator ?? new DefaultSlabAllocator();
        _diagnosticBus = diagnosticBus;
        var firstBlock = _allocator.Allocate(DefaultBlockSize, pinned: false);
        _blocks.Add(firstBlock, 0);
        _totalCapacity = DefaultBlockSize;
    }

    /// <inheritdoc />
    public long Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _totalCapacity;
    }

    /// <inheritdoc />
    public long Position
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (long)_baseOffset + _blocks[_currentBlockIndex].Slab.Offset;
    }

    /// <inheritdoc />
    public int SlabCount => _blocks.Count;

    /// <inheritdoc />
    public long TotalAllocatedBytes => _totalCapacity;

    /// <summary>
    /// Allocates a contiguous block of memory of the specified size.
    /// </summary>
    /// <param name="size">The number of bytes to allocate.</param>
    /// <returns>A memory block of the requested size.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<byte> Allocate(int size) => Allocate(size, 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<byte> Allocate(int size, int alignment)
    {
        if (alignment < 1) alignment = 1;
        var currentBlock = _blocks[_currentBlockIndex].Slab;
        int alignedOffset = (currentBlock.Offset + alignment - 1) & ~(alignment - 1);

        if (alignedOffset + size > currentBlock.Capacity)
        {
            PrepareBlock(size + alignment);
            currentBlock = _blocks[_currentBlockIndex].Slab;
            alignedOffset = (currentBlock.Offset + alignment - 1) & ~(alignment - 1);
        }

        var memory = new Memory<byte>(currentBlock.Data, alignedOffset, size);
        currentBlock.Offset = alignedOffset + size;
        return memory;
    }

    /// <inheritdoc />
    public void Advance(int count)
    {
        if (count == 0) return;
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

        var block = _blocks[_currentBlockIndex].Slab;
        if (block.Offset + count > block.Capacity)
            throw new InvalidOperationException("Cannot advance past current block capacity.");

        block.Offset += count;
    }

    /// <inheritdoc />
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        PrepareBlock(sizeHint);
        var block = _blocks[_currentBlockIndex].Slab;
        return block.Data.AsMemory(block.Offset, block.Capacity - block.Offset);
    }

    /// <inheritdoc />
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        PrepareBlock(sizeHint);
        var block = _blocks[_currentBlockIndex].Slab;
        return block.Data.AsSpan(block.Offset, block.Capacity - block.Offset);
    }

    private void PrepareBlock(int sizeHint)
    {
        if (sizeHint < 0) throw new ArgumentOutOfRangeException(nameof(sizeHint));
        if (sizeHint == 0) sizeHint = 1;

        var currentEntry = _blocks[_currentBlockIndex];
        if (currentEntry.Slab.Offset + sizeHint > currentEntry.Slab.Capacity)
        {
            // Use current slab's Capacity to define a fixed-size virtual address space.
            long nextBaseOffset = currentEntry.BaseOffset + currentEntry.Slab.Capacity;

            if (sizeHint > DefaultBlockSize)
            {
                var oversized = _allocator.Allocate(sizeHint, pinned: false, isOversized: true);
                _currentBlockIndex++;
                _blocks.Insert(_currentBlockIndex, oversized, nextBaseOffset);
                _totalCapacity += oversized.Capacity;
                _baseOffset = nextBaseOffset;
            }
            else
            {
                _baseOffset = nextBaseOffset;
                _currentBlockIndex++;
                if (_currentBlockIndex >= _blocks.Count)
                {
                    var newBlock = _allocator.Allocate(DefaultBlockSize, pinned: false);
                    _blocks.Add(newBlock, _baseOffset);
                    _totalCapacity += newBlock.Capacity;
                }
                else
                {
                    var entry = _blocks[_currentBlockIndex];
                    entry.Slab.Offset = 0;
                    entry.BaseOffset = _baseOffset;
                    _blocks[_currentBlockIndex] = entry;
                }
            }
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Shrink()
    {
        // Reclaim all blocks beyond the current one
        _blocks.Prune(_currentBlockIndex + 1, _allocator);

        // Recalculate total capacity
        _totalCapacity = 0;
        for (int i = 0; i < _blocks.Count; i++)
        {
            _totalCapacity += _blocks[i].Slab.Capacity;
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetSegmentAsSpan(long offset, int length)
    {
        var spans = _blocks.AsSpan();
        int low = 0;
        int high = spans.Length - 1;
        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            ref readonly var entry = ref spans[mid];
            if (offset >= entry.BaseOffset && offset < entry.BaseOffset + entry.Slab.Capacity)
            {
                if (offset + length > entry.BaseOffset + entry.Slab.Capacity)
                    throw new ArgumentException("Segment spans across multiple slabs.");
                return entry.Slab.Data.AsSpan((int)(offset - entry.BaseOffset), length);
            }
            if (offset < entry.BaseOffset)
                high = mid - 1;
            else
                low = mid + 1;
        }
        throw new ArgumentOutOfRangeException(nameof(offset));
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetMutableSegmentAsSpan(long offset, int length)
    {
        var spans = _blocks.AsSpan();
        int low = 0;
        int high = spans.Length - 1;
        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            ref readonly var entry = ref spans[mid];
            if (offset >= entry.BaseOffset && offset < entry.BaseOffset + entry.Slab.Capacity)
            {
                if (offset + length > entry.BaseOffset + entry.Slab.Capacity)
                    throw new ArgumentException("Segment spans across multiple slabs.");
                return entry.Slab.Data.AsSpan((int)(offset - entry.BaseOffset), length);
            }
            if (offset < entry.BaseOffset)
                high = mid - 1;
            else
                low = mid + 1;
        }
        throw new ArgumentOutOfRangeException(nameof(offset));
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _currentBlockIndex = 0;
        _baseOffset = 0;

        _blocks.Prune(64, _allocator);

        for (int i = _blocks.Count - 1; i >= 0; i--)
        {
            if (_blocks[i].Slab.IsOversized)
            {
                _allocator.Return(_blocks[i].Slab);
                _blocks.RemoveAt(i);
            }
            else
            {
                var entry = _blocks[i];
                entry.Slab.Offset = 0;
                _blocks[i] = entry;
            }
        }

        _totalCapacity = 0;
        long currentBase = 0;
        for (int i = 0; i < _blocks.Count; i++)
        {
            var entry = _blocks[i];
            entry.BaseOffset = currentBase;
            _blocks[i] = entry;
            _totalCapacity += entry.Slab.Capacity;
            currentBase += entry.Slab.Capacity;
        }

        if (_blocks.Count == 0)
        {
            var firstBlock = _allocator.Allocate(DefaultBlockSize, pinned: false);
            _blocks.Add(firstBlock, 0);
            _totalCapacity = DefaultBlockSize;
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        foreach (var entry in _blocks) _allocator.Return(entry.Slab);
        _blocks.Clear();
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> GetDiagnosticInfo()
    {
        var info = new Dictionary<string, object>
        {
            ["Capacity"] = Capacity,
            ["Position"] = Position,
            ["SlabCount"] = SlabCount,
            ["TotalAllocatedBytes"] = TotalAllocatedBytes
        };

        _diagnosticBus?.Publish("Buffer", "ArenaAllocator Stats", info, (m, state) =>
        {
            foreach (var kvp in state) m.Add(kvp.Key, kvp.Value.ToString() ?? string.Empty);
        });

        return info;
    }
}
