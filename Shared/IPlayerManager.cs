using LiteNetLib;

namespace Shared
{
    public interface IPlayerManager
    {
        void OnPeerConnected(NetPeer peer);
        void OnPeerDisconnected(NetPeer peer);
    }
}
