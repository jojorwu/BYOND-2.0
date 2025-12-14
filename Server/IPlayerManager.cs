using System.Collections.Generic;
using LiteNetLib;
using Shared;

namespace Server
{
    public interface IPlayerManager
    {
        void AddPlayer(NetPeer peer);
        void RemovePlayer(NetPeer peer);
        IEnumerable<NetPeer> GetPlayersInRegion(Region region);
    }
}
