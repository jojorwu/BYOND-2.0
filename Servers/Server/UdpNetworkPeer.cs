using Shared;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Threading.Tasks;

namespace Server
{
    public class UdpNetworkPeer : INetworkPeer
    {
        private readonly NetPeer _peer;
        private readonly NetDataWriterPool _writerPool;
        public System.Collections.Generic.IDictionary<long, long> LastSentVersions { get; } = new System.Collections.Concurrent.ConcurrentDictionary<long, long>();

        public UdpNetworkPeer(NetPeer peer, NetDataWriterPool writerPool)
        {
            _peer = peer;
            _writerPool = writerPool;
        }

        public ValueTask SendAsync(string data)
        {
            var writer = _writerPool.Rent();
            try
            {
                writer.Put(data);
                _peer.Send(writer, DeliveryMethod.Unreliable);
            }
            finally
            {
                _writerPool.Return(writer);
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask SendAsync(System.ReadOnlyMemory<byte> data)
        {
            var writer = _writerPool.Rent();
            try
            {
                if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(data, out var segment))
                {
                    writer.Put(segment.Array, segment.Offset, segment.Count);
                }
                else
                {
                    writer.Put(data.ToArray());
                }
                _peer.Send(writer, DeliveryMethod.Unreliable);
            }
            finally
            {
                _writerPool.Return(writer);
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask SendAsync(byte[] data)
        {
            var writer = _writerPool.Rent();
            try
            {
                writer.Put(data);
                _peer.Send(writer, DeliveryMethod.Unreliable);
            }
            finally
            {
                _writerPool.Return(writer);
            }
            return ValueTask.CompletedTask;
        }
    }
}
