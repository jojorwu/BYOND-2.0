using System.Collections.Concurrent;
using LiteNetLib.Utils;
using Shared.Services;

namespace Server
{
    public class NetDataWriterPool : EngineService
    {
        public override int Priority => 50;
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
