using System.Collections.Concurrent;
using LiteNetLib.Utils;
using Shared.Interfaces;
using Shared.Services;

namespace Server
{
    /// <summary>
    /// Provides a thread-safe pool of NetDataWriter instances to reduce allocations
    /// during high-frequency network operations.
    /// </summary>
    public class NetDataWriterPool : EngineService, IObjectPool<NetDataWriter>
    {
        public override int Priority => 50;
        private readonly ConcurrentQueue<NetDataWriter> _pool = new();

        /// <summary>
        /// Retrieves a writer from the pool or creates a new one if the pool is empty.
        /// </summary>
        public NetDataWriter Rent()
        {
            if (_pool.TryDequeue(out var writer))
            {
                writer.Reset();
                return writer;
            }
            return new NetDataWriter();
        }

        /// <summary>
        /// Returns a writer to the pool for future reuse.
        /// </summary>
        public void Return(NetDataWriter writer)
        {
            _pool.Enqueue(writer);
        }

        public void Shrink()
        {
            int count = _pool.Count;
            for (int i = 0; i < count / 2; i++)
            {
                _pool.TryDequeue(out _);
            }
        }
    }
}
