using System;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Services
{
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

        public Memory<byte> Allocate(int size)
        {
            var currentBlock = _blocks[_currentBlockIndex];
            if (currentBlock.Offset + size > currentBlock.Data.Length)
            {
                _currentBlockIndex++;
                if (_currentBlockIndex >= _blocks.Count)
                {
                    _blocks.Add(new Block(Math.Max(DefaultBlockSize, size)));
                }
                currentBlock = _blocks[_currentBlockIndex];

                if (size > currentBlock.Data.Length)
                {
                     var oversized = new Block(size);
                     _blocks.Insert(_currentBlockIndex, oversized);
                     currentBlock = oversized;
                }
            }

            var memory = new Memory<byte>(currentBlock.Data, currentBlock.Offset, size);
            currentBlock.Offset += size;
            return memory;
        }

        public void Reset()
        {
            foreach (var block in _blocks)
            {
                block.Offset = 0;
            }
            _currentBlockIndex = 0;
        }
    }
}
