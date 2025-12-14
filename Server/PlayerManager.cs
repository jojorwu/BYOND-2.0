using System.Collections.Generic;
using LiteNetLib;
using Shared;

namespace Server
{
    public class PlayerManager : IPlayerManager
    {
        private readonly Dictionary<NetPeer, IGameObject> _players = new();
        private readonly IObjectApi _objectApi;
        private readonly IObjectTypeManager _objectTypeManager;
        private readonly ServerSettings _settings;

        public PlayerManager(IObjectApi objectApi, IObjectTypeManager objectTypeManager, ServerSettings settings)
        {
            _objectApi = objectApi;
            _objectTypeManager = objectTypeManager;
            _settings = settings;
        }

        public void AddPlayer(NetPeer peer)
        {
            var playerObjectType = _objectTypeManager.GetObjectType(_settings.PlayerObjectTypePath);
            if (playerObjectType != null)
            {
                var playerObject = _objectApi.CreateObject(playerObjectType.Id, 0, 0, 0);
                if(playerObject != null)
                    _players[peer] = playerObject;
            }
        }

        public void RemovePlayer(NetPeer peer)
        {
            if(_players.TryGetValue(peer, out var playerObject))
            {
                _objectApi.DestroyObject(playerObject.Id);
                _players.Remove(peer);
            }
        }

        public IEnumerable<NetPeer> GetPlayersInRegion(Region region)
        {
            var regionObjects = new HashSet<IGameObject>(region.GetGameObjects());
            foreach (var (peer, playerObject) in _players)
            {
                if (regionObjects.Contains(playerObject))
                {
                    yield return peer;
                }
            }
        }
    }
}
