using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;

namespace Shared.Buffers;

/// <summary>
/// A fast arena-based memory allocator that utilizes multiple slabs of memory.
/// Suitable for high-frequency allocations that are all reclaimed at once.
/// </summary>
public class ArenaAllocator : IBuffer, IArenaAllocator, IDisposable
{
    private const int DefaultBlockSize = 1024 * 1024; // 1MB
    private readonly List<BufferSlab> _blocks = new();
    private int _currentBlockIndex;
    private int _baseOffset;

    public ArenaAllocator()
    {
        _blocks.Add(new BufferSlab(DefaultBlockSize, fromPool: true, pinned: false));
    }

    /// <inheritdoc />
    public int Capacity => _blocks.Sum(b => b.Capacity);

    /// <inheritdoc />
    public int Position => _baseOffset + _blocks[_currentBlockIndex].Offset;

    /// <summary>
    /// Allocates a contiguous block of memory of the specified size.
    /// </summary>
    /// <param name="size">The number of bytes to allocate.</param>
    /// <returns>A memory block of the requested size.</returns>
    public Memory<byte> Allocate(int size) => Allocate(size, 1);

    public Memory<byte> Allocate(int size, int alignment)
    {
        if (alignment < 1) alignment = 1;
        var currentBlock = _blocks[_currentBlockIndex];
        int alignedOffset = (currentBlock.Offset + alignment - 1) & ~(alignment - 1);

        if (alignedOffset + size > currentBlock.Capacity)
        {
            _baseOffset += currentBlock.Capacity;
            _currentBlockIndex++;
            if (_currentBlockIndex >= _blocks.Count)
            {
                _blocks.Add(new BufferSlab(Math.Max(DefaultBlockSize, size + alignment), fromPool: true, pinned: false));
            }
            currentBlock = _blocks[_currentBlockIndex];
            alignedOffset = (currentBlock.Offset + alignment - 1) & ~(alignment - 1);

            if (alignedOffset + size > currentBlock.Capacity)
            {
                 var oversized = new BufferSlab(size + alignment, fromPool: false, pinned: false, isOversized: true);
                 _blocks.Insert(_currentBlockIndex, oversized);
                 currentBlock = oversized;
                 alignedOffset = 0;
                 // Base offset does not change as the oversized slab starts at the current _baseOffset.
                 // Subsequent blocks will have their base offsets pushed further.
            }
        }
        var memory = new Memory<byte>(currentBlock.Data, alignedOffset, size);
        currentBlock.Offset = alignedOffset + size;
        return memory;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _currentBlockIndex = 0;
        _baseOffset = 0;

        if (_blocks.Count > 64)
        {
            for (int i = 64; i < _blocks.Count; i++) _blocks[i].Dispose();
            _blocks.RemoveRange(64, _blocks.Count - 64);
        }

        for (int i = _blocks.Count - 1; i >= 0; i--)
        {
            if (_blocks[i].IsOversized)
            {
                _blocks[i].Dispose();
                _blocks.RemoveAt(i);
            }
            else
            {
                _blocks[i].Offset = 0;
            }
        }

        if (_blocks.Count == 0)
        {
            _blocks.Add(new BufferSlab(DefaultBlockSize, fromPool: true, pinned: false));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var block in _blocks) block.Dispose();
        _blocks.Clear();
    }
}
