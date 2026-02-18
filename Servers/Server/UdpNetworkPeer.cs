using Shared;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Server
{
    public class UdpNetworkPeer : INetworkPeer
    {
        private readonly NetPeer _peer;
        private readonly NetDataWriterPool _writerPool;
        public System.Collections.Generic.IDictionary<int, long> LastSentVersions { get; } = new System.Collections.Concurrent.ConcurrentDictionary<int, long>();

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

        public void PruneLastSentVersions(IEnumerable<int> activeIds)
        {
            var activeSet = activeIds as HashSet<int> ?? activeIds.ToHashSet();
            var keysToRemove = LastSentVersions.Keys.Where(k => !activeSet.Contains(k)).ToList();
            foreach (var key in keysToRemove)
            {
                LastSentVersions.Remove(key);
            }
        }
    }
}
