using System;
using System.Buffers;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Services;
    public class ArenaAllocator : IArenaAllocator, IDisposable
    {
        private const int DefaultBlockSize = 1024 * 1024; // 1MB

        private class Block
        {
            public readonly byte[] Data;
            public int Offset;
            public readonly bool FromPool;

            public Block(int size, bool fromPool)
            {
                Data = fromPool ? ArrayPool<byte>.Shared.Rent(size) : new byte[size];
                Offset = 0;
                FromPool = fromPool;
            }

            public void Return()
            {
                if (FromPool) ArrayPool<byte>.Shared.Return(Data);
            }
        }

        private readonly List<Block> _blocks = new();
        private int _currentBlockIndex;

        public ArenaAllocator()
        {
            _blocks.Add(new Block(DefaultBlockSize, true));
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
                    _blocks.Add(new Block(Math.Max(DefaultBlockSize, size + alignment), true));
                }
                currentBlock = _blocks[_currentBlockIndex];
                alignedOffset = (currentBlock.Offset + alignment - 1) & ~(alignment - 1);

                if (alignedOffset + size > currentBlock.Data.Length)
                {
                     var oversized = new Block(size + alignment, false);
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
            if (_blocks.Count > 1024)
            {
                for (int i = 1024; i < _blocks.Count; i++)
                {
                    _blocks[i].Return();
                }
                _blocks.RemoveRange(1024, _blocks.Count - 1024);
            }

            _currentBlockIndex = 0;
            foreach (var block in _blocks)
            {
                block.Offset = 0;
            }
        }

        public void Dispose()
        {
            foreach (var block in _blocks)
            {
                block.Return();
            }
            _blocks.Clear();
        }
    }
