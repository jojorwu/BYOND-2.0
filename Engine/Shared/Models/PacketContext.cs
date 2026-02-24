using Shared.Interfaces;

using System;
using Shared.Interfaces;

namespace Shared.Models;
    /// <summary>
    /// Context for a network packet being processed by the middleware pipeline.
    /// </summary>
    public class PacketContext
    {
        public INetworkPeer Peer { get; set; }
        public byte TypeId { get; set; }
        public ReadOnlyMemory<byte> Payload { get; set; }
        public string PayloadString => System.Text.Encoding.UTF8.GetString(Payload.Span);

        public PacketContext(INetworkPeer peer, byte typeId, ReadOnlyMemory<byte> payload)
        {
            Peer = peer;
            TypeId = typeId;
            Payload = payload;
        }

        [Obsolete("Use ReadOnlyMemory version")]
        public PacketContext(INetworkPeer peer, byte typeId, string payload)
        {
            Peer = peer;
            TypeId = typeId;
            Payload = System.Text.Encoding.UTF8.GetBytes(payload).AsMemory();
        }
    }
