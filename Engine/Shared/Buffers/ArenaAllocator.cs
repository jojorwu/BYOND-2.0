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
public class ArenaAllocator : IBuffer, IArenaAllocator, IDisposable
{
    private const int DefaultBlockSize = 1024 * 1024; // 1MB
    private SlabList _blocks = new();
    private int _currentBlockIndex;
    private int _baseOffset;
    private int _totalCapacity;
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
    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _totalCapacity;
    }

    /// <inheritdoc />
    public int Position
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _baseOffset + _blocks[_currentBlockIndex].Slab.Offset;
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
            _baseOffset += currentBlock.Capacity;
            _currentBlockIndex++;
            if (_currentBlockIndex >= _blocks.Count)
            {
                var newBlock = _allocator.Allocate(Math.Max(DefaultBlockSize, size + alignment), pinned: false);
                _blocks.Add(newBlock, _baseOffset);
                _totalCapacity += newBlock.Capacity;
            }
            currentBlock = _blocks[_currentBlockIndex].Slab;
            alignedOffset = (currentBlock.Offset + alignment - 1) & ~(alignment - 1);

            if (alignedOffset + size > currentBlock.Capacity)
            {
                 var oversized = _allocator.Allocate(size + alignment, pinned: false, isOversized: true);
                 _blocks.Insert(_currentBlockIndex, oversized, _baseOffset);
                 _totalCapacity += oversized.Capacity;
                 currentBlock = oversized;
                 alignedOffset = 0;
            }
        }
        var memory = new Memory<byte>(currentBlock.Data, alignedOffset, size);
        currentBlock.Offset = alignedOffset + size;
        return memory;
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
                _blocks[i].Slab.Offset = 0;
            }
        }

        _totalCapacity = 0;
        for (int i = 0; i < _blocks.Count; i++)
        {
            var entry = _blocks[i];
            entry.BaseOffset = _totalCapacity;
            _totalCapacity += entry.Slab.Capacity;
            _blocks[i] = entry;
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
