using Shared;
using LiteNetLib;
using LiteNetLib.Utils;

namespace Server
{
    public class UdpNetworkPeer : INetworkPeer
    {
        private readonly NetPeer _peer;
        private readonly NetDataWriterPool _writerPool;

        public UdpNetworkPeer(NetPeer peer, NetDataWriterPool writerPool)
        {
            _peer = peer;
            _writerPool = writerPool;
        }

        public void Send(string data)
        {
            var writer = _writerPool.Get();
            try
            {
                writer.Put(data);
                _peer.Send(writer, DeliveryMethod.Unreliable);
            }
            finally
            {
                _writerPool.Return(writer);
            }
        }
    }
}
