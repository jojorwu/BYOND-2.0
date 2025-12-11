using LiteNetLib;
using Robust.Shared.Maths;

namespace Shared
{
    public interface IUdpServer
    {
        void BroadcastSnapshot(string snapshot);
        void BroadcastSnapshot(Region region, string snapshot);
        void UpdatePeerRegion(NetPeer peer, Vector2i regionCoords);
    }
}
