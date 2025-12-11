using LiteNetLib;

namespace Server
{
    public interface IPlayerManager
    {
        void OnPeerConnected(NetPeer peer);
        void OnPeerDisconnected(NetPeer peer);
    }
}
