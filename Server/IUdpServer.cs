using LiteNetLib;
using Robust.Shared.Maths;
using Shared;

namespace Server
{
    public interface IUdpServer
    {
        void BroadcastSnapshot(string snapshot);
        void BroadcastSnapshot(Region region, string snapshot);
        void UpdatePeerRegion(NetPeer peer, Vector2i regionCoords);
    }
}
