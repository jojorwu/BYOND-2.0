using Shared.Interfaces;

namespace Shared.Models;
    /// <summary>
    /// Context for a network packet being processed by the middleware pipeline.
    /// </summary>
    public class PacketContext
    {
        public INetworkPeer Peer { get; set; }
        public byte TypeId { get; set; }
        public string Payload { get; set; }

        public PacketContext(INetworkPeer peer, byte typeId, string payload)
        {
            Peer = peer;
            TypeId = typeId;
            Payload = payload;
        }
    }
