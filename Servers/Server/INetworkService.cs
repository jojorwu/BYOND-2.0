using System;
using LiteNetLib;
using Shared;

namespace Server
{
    public interface INetworkService
    {
        event Action<INetworkPeer> PeerConnected;
        event Action<INetworkPeer, DisconnectInfo> PeerDisconnected;

        void Start();
        void Stop();
        void BroadcastSnapshot(string snapshot);
        void BroadcastSnapshot(byte[] snapshot);
    }
}
