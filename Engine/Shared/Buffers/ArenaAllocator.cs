using System;
using System.Buffers;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Buffers;

public class ArenaAllocator : IArenaAllocator, IDisposable
{
    private const int DefaultBlockSize = 1024 * 1024; // 1MB
    private readonly List<BufferSlab> _blocks = new();
    private int _currentBlockIndex;

    public ArenaAllocator()
    {
        _blocks.Add(new BufferSlab(DefaultBlockSize, fromPool: true, pinned: false));
    }

    public Memory<byte> Allocate(int size) => Allocate(size, 1);

    public Memory<byte> Allocate(int size, int alignment)
    {
        if (alignment < 1) alignment = 1;
        var currentBlock = _blocks[_currentBlockIndex];
        int alignedOffset = (currentBlock.Offset + alignment - 1) & ~(alignment - 1);

        if (alignedOffset + size > currentBlock.Capacity)
        {
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
            }
        }
        var memory = new Memory<byte>(currentBlock.Data, alignedOffset, size);
        currentBlock.Offset = alignedOffset + size;
        return memory;
    }

    public void Reset()
    {
        if (_blocks.Count > 64)
        {
            for (int i = 64; i < _blocks.Count; i++) _blocks[i].Dispose();
            _blocks.RemoveRange(64, _blocks.Count - 64);
        }
        _currentBlockIndex = 0;
        foreach (var block in _blocks) block.Offset = 0;
    }

    public void Dispose()
    {
        foreach (var block in _blocks) block.Dispose();
        _blocks.Clear();
    }
}
