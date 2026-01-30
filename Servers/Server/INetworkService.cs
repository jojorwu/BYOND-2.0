using System;
using LiteNetLib;
using Shared;

namespace Server
{
    public interface INetworkService
    {
        event Action<INetworkPeer> PeerConnected;
        event Action<INetworkPeer, DisconnectInfo> PeerDisconnected;
        event Action<INetworkPeer, string> CommandReceived;

        void Start();
        void Stop();
        void BroadcastSnapshot(string snapshot);
    }
}
