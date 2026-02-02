using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;

namespace Shared.Interfaces
{
    public interface IPlayerManager
    {
        void AddPlayer(INetworkPeer peer);
        void RemovePlayer(INetworkPeer peer);
        void ForEachPlayerInRegion(Region region, Action<INetworkPeer> action);
        void ForEachPlayerObject(Action<IGameObject> action);
    }
}
