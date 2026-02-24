using System;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Services;
    public class ArenaAllocator : IArenaAllocator
    {
        private const int DefaultBlockSize = 64 * 1024; // 64KB

        private class Block
        {
            public readonly byte[] Data;
            public int Offset;

            public Block(int size)
            {
                Data = new byte[size];
                Offset = 0;
            }
        }

        private readonly List<Block> _blocks = new();
        private int _currentBlockIndex;

        public ArenaAllocator()
        {
            _blocks.Add(new Block(DefaultBlockSize));
        }

        public Memory<byte> Allocate(int size) => Allocate(size, 1);

        public Memory<byte> Allocate(int size, int alignment)
        {
            if (alignment < 1) alignment = 1;

            var currentBlock = _blocks[_currentBlockIndex];
            int alignedOffset = (currentBlock.Offset + alignment - 1) & ~(alignment - 1);

            if (alignedOffset + size > currentBlock.Data.Length)
            {
                _currentBlockIndex++;
                if (_currentBlockIndex >= _blocks.Count)
                {
                    _blocks.Add(new Block(Math.Max(DefaultBlockSize, size + alignment)));
                }
                currentBlock = _blocks[_currentBlockIndex];
                alignedOffset = (currentBlock.Offset + alignment - 1) & ~(alignment - 1);

                if (alignedOffset + size > currentBlock.Data.Length)
                {
                     var oversized = new Block(size + alignment);
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
            // If we have many blocks, prune them to reclaim memory
            if (_blocks.Count > 8)
            {
                _blocks.RemoveRange(8, _blocks.Count - 8);
            }

            foreach (var block in _blocks)
            {
                block.Offset = 0;
            }
            _currentBlockIndex = 0;
        }
    }
