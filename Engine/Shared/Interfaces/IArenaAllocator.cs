using System;

namespace Shared.Interfaces
{
    /// <summary>
    /// Provides high-performance, temporary memory allocation for transient data.
    /// </summary>
    public interface IArenaAllocator
    {
        /// <summary>
        /// Allocates a chunk of memory from the arena.
        /// </summary>
        Memory<byte> Allocate(int size);

        /// <summary>
        /// Resets the arena, reclaiming all allocated memory.
        /// </summary>
        void Reset();
    }
}
