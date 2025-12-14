using System.Collections.Generic;

namespace Shared
{
    public interface IPlayerManager
    {
        void AddPlayer(INetworkPeer peer);
        void RemovePlayer(INetworkPeer peer);
        IEnumerable<INetworkPeer> GetPlayersInRegion(Region region);
        IEnumerable<IGameObject> GetAllPlayerObjects();
    }
}
