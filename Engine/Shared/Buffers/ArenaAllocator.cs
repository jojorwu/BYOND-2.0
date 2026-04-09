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
    private SlabLookupCache _lookupCache = new();
    private long _baseOffset;
    private long _totalCapacity;
    private int _nextBlockSize;
    private readonly ISlabAllocator _allocator;
    private readonly IDiagnosticBus? _diagnosticBus;

    public ArenaAllocator(ISlabAllocator? allocator = null, IDiagnosticBus? diagnosticBus = null)
    {
        _allocator = allocator ?? new DefaultSlabAllocator();
        _diagnosticBus = diagnosticBus;
        _nextBlockSize = DefaultBlockSize;
        var firstBlock = _allocator.Allocate(_nextBlockSize, pinned: false);
        _blocks.Add(firstBlock, 0);
        _totalCapacity = firstBlock.Capacity;
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
    public long Length => Position;

    /// <inheritdoc />
    public ReadOnlySequence<byte> WrittenSequence
    {
        get
        {
            var builder = new SequenceBuilder();
            for (int i = 0; i <= _currentBlockIndex; i++)
            {
                var entry = _blocks[i];
                builder.Add(new ReadOnlyMemory<byte>(entry.Slab.Data, 0, entry.Slab.Offset));
            }
            return builder.Build();
        }
    }

    /// <inheritdoc />
    public int SlabCount => _blocks.Count;

    /// <inheritdoc />
    public long TotalAllocatedBytes => _totalCapacity;

    /// <inheritdoc />
    public bool IsPinned => false;

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
        if (sizeHint < 0) throw new ArgumentOutOfRangeException(nameof(sizeHint), "Size hint must be non-negative.");
        if (sizeHint == 0) sizeHint = 1;

        var currentEntry = _blocks[_currentBlockIndex];
        if (currentEntry.Slab.Offset + sizeHint > currentEntry.Slab.Capacity)
        {
            // Use current slab's Capacity to define a fixed-size virtual address space.
            long nextBaseOffset = currentEntry.BaseOffset + currentEntry.Slab.Capacity;

            if (sizeHint > _nextBlockSize)
            {
                var oversized = _allocator.Allocate(sizeHint, pinned: false, isOversized: true);
                try
                {
                    _currentBlockIndex++;
                    _blocks.Insert(_currentBlockIndex, oversized, nextBaseOffset);
                    _totalCapacity += oversized.Capacity;
                    _baseOffset = nextBaseOffset;
                }
                catch
                {
                    _allocator.Return(oversized);
                    throw;
                }
            }
            else
            {
                _baseOffset = nextBaseOffset;
                _currentBlockIndex++;
                if (_currentBlockIndex >= _blocks.Count)
                {
                    var newBlock = _allocator.Allocate(_nextBlockSize, pinned: false);
                    try
                    {
                        _blocks.Add(newBlock, _baseOffset);
                        _totalCapacity += newBlock.Capacity;
                    }
                    catch
                    {
                        _allocator.Return(newBlock);
                        throw;
                    }
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
    public ReadOnlySpan<byte> GetSegmentAsSpan(long offset, int length) => GetSegmentInternal(offset, length);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetMutableSegmentAsSpan(long offset, int length) => GetSegmentInternal(offset, length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<byte> GetSegmentInternal(long offset, int length)
    {
        if (_lookupCache.TryResolve(offset, length, _blocks, out Span<byte> result)) return result;

        int index = _blocks.FindSlabIndex(offset);
        if (index >= 0)
        {
            _lookupCache.Update(index);
            return _blocks.GetSegmentAsSpan(offset, length);
        }
        throw new InvalidBufferOffsetException($"Offset {offset} is outside of the buffer's address space.");
    }

    /// <inheritdoc />
    public void CopyTo(System.IO.Stream destination)
    {
        for (int i = 0; i <= _currentBlockIndex; i++)
        {
            var entry = _blocks[i];
            destination.Write(entry.Slab.Data.AsSpan(0, entry.Slab.Offset));
        }
    }

    /// <inheritdoc />
    public void CopyTo(Span<byte> destination)
    {
        int totalWritten = 0;
        for (int i = 0; i <= _currentBlockIndex; i++)
        {
            var entry = _blocks[i];
            entry.Slab.Data.AsSpan(0, entry.Slab.Offset).CopyTo(destination.Slice(totalWritten));
            totalWritten += entry.Slab.Offset;
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => Reset(resizeHeuristics: true);

    /// <summary>
    /// Resets the allocator, optionally adjusting the initial block size based on current usage.
    /// </summary>
    public void Reset(bool resizeHeuristics)
    {
        if (resizeHeuristics)
        {
            if (_blocks.Count > 2)
            {
                // Increase next initial size to half of total usage, up to 128MB
                _nextBlockSize = (int)Math.Clamp(_totalCapacity / 2, DefaultBlockSize, 128 * 1024 * 1024);
            }
            else if (_blocks.Count == 1 && _blocks[0].Slab.Offset < DefaultBlockSize / 4)
            {
                // Optionally could shrink, but we stay at DefaultBlockSize minimum
                _nextBlockSize = DefaultBlockSize;
            }
        }

        _currentBlockIndex = 0;
        _lookupCache.Reset();
        _baseOffset = 0;

        // Prune excessive slabs to prevent memory bloating
        _blocks.Prune(64, _allocator);

        // Remove oversized blocks and reset offsets for pooled blocks
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

        // Re-calculate total capacity and reconstruct fixed virtual address space
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

        // Ensure at least one block exists
        if (_blocks.Count == 0)
        {
            var firstBlock = _allocator.Allocate(_nextBlockSize, pinned: false);
            _blocks.Add(firstBlock, 0);
            _totalCapacity = firstBlock.Capacity;
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        foreach (var entry in _blocks)
        {
            if (entry.RefCountedSlab != null) entry.RefCountedSlab.Dispose();
            else _allocator.Return(entry.Slab);
        }
        _blocks.Clear();
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> GetDiagnosticInfo()
    {
        long pooledBytes = 0;
        foreach (var entry in _blocks) if (entry.Slab.IsFromPool) pooledBytes += entry.Slab.Capacity;

        var info = new Dictionary<string, object>
        {
            ["Capacity"] = Capacity,
            ["Position"] = Position,
            ["Length"] = Length,
            ["SlabCount"] = SlabCount,
            ["TotalAllocatedBytes"] = TotalAllocatedBytes,
            ["PooledBytes"] = pooledBytes,
            ["IsPinned"] = IsPinned
        };

        _diagnosticBus?.Publish("Buffer", "ArenaAllocator Stats", info, (m, state) =>
        {
            foreach (var kvp in state) m.Add(kvp.Key, kvp.Value.ToString() ?? string.Empty);
        });

        return info;
    }
}
