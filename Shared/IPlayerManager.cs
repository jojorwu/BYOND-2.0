using System.Collections.Generic;

namespace Shared
{
    public interface IPlayerManager
    {
        void AddPlayer(INetworkPeer peer);
        void RemovePlayer(INetworkPeer peer);
        void ForEachPlayerInRegion(Region region, Action<INetworkPeer> action);
        void ForEachPlayerObject(Action<IGameObject> action);
    }
}
