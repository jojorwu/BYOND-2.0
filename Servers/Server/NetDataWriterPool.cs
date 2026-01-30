using System.Collections.Concurrent;
using LiteNetLib.Utils;

namespace Server
{
    public class NetDataWriterPool
    {
        private readonly ConcurrentQueue<NetDataWriter> _pool = new();

        public NetDataWriter Get()
        {
            if (_pool.TryDequeue(out var writer))
            {
                writer.Reset();
                return writer;
            }
            return new NetDataWriter();
        }

        public void Return(NetDataWriter writer)
        {
            _pool.Enqueue(writer);
        }
    }
}
