using Shared;
using Shared.Interfaces;
using LiteNetLib;

namespace Server.Events
{
    public record PeerConnectedEvent(INetworkPeer Peer);
    public record PeerDisconnectedEvent(INetworkPeer Peer, DisconnectInfo DisconnectInfo);
    public record CommandReceivedEvent(INetworkPeer Peer, string Command);
    public record ReloadScriptsEvent;
}
