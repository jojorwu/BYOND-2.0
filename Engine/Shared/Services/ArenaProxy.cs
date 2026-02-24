using System;
using Shared.Interfaces;

namespace Shared.Services;
    public class ArenaProxy : IArenaAllocator
    {
        private readonly IJobSystem _jobSystem;

        public ArenaProxy(IJobSystem jobSystem)
        {
            _jobSystem = jobSystem;
        }

        public Memory<byte> Allocate(int size) => Allocate(size, 1);

        public Memory<byte> Allocate(int size, int alignment)
        {
            var arena = _jobSystem.GetCurrentArena();
            if (arena == null)
            {
                // Fallback for non-worker threads (e.g. main thread during initialization)
                // In a production engine we'd probably want a separate main-thread arena
                return new byte[size];
            }
            return arena.Allocate(size, alignment);
        }

        public void Reset()
        {
            _jobSystem.GetCurrentArena()?.Reset();
        }
    }
