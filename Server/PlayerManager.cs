using Shared;
using LiteNetLib;
using System.Collections.Concurrent;
using Robust.Shared.Maths;

namespace Server
{
    public class PlayerManager : IPlayerManager
    {
        private readonly IGameState _gameState;
        private readonly IUdpServer _udpServer;
        private readonly IObjectApi _objectApi;
        private readonly ConcurrentDictionary<NetPeer, IGameObject> _playerObjects = new();

        public PlayerManager(IGameState gameState, IUdpServer udpServer, IObjectApi objectApi)
        {
            _gameState = gameState;
            _udpServer = udpServer;
            _objectApi = objectApi;
        }

        public void OnPeerConnected(NetPeer peer)
        {
            // For now, we'll create a generic "player" object type.
            // A more robust solution would be to have a configurable player object type.
            var playerObjectType = 1; // Assuming 1 is a valid object type for a player
            var playerObject = _objectApi.CreateObject(playerObjectType, 0, 0, 0);
            if (playerObject != null)
            {
                _playerObjects[peer] = playerObject;
                var (chunkCoords, _) = Map.GlobalToChunk(playerObject.X, playerObject.Y);
                _udpServer.UpdatePeerRegion(peer, chunkCoords);
            }
        }

        public void OnPeerDisconnected(NetPeer peer)
        {
            if (_playerObjects.TryRemove(peer, out var playerObject))
            {
                _objectApi.DestroyObject(playerObject.Id);
            }
        }
    }
}
