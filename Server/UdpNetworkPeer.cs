using Shared;
using LiteNetLib;

namespace Server
{
    public class UdpNetworkPeer : INetworkPeer
    {
        private readonly NetPeer _peer;

        public UdpNetworkPeer(NetPeer peer)
        {
            _peer = peer;
        }

        public void Send(string data)
        {
            var writer = new LiteNetLib.Utils.NetDataWriter();
            writer.Put(data);
            _peer.Send(writer, DeliveryMethod.Unreliable);
        }
    }
}
