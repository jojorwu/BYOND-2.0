using Shared;
using LiteNetLib;
using System.Collections.Concurrent;
using Robust.Shared.Maths;
using Server;

namespace Server
{
    public class PlayerManager : IPlayerManager
    {
        private readonly IGameState _gameState;
        private readonly IUdpServer _udpServer;
        private readonly IObjectApi _objectApi;
        private readonly IObjectTypeManager _objectTypeManager;
        private readonly ConcurrentDictionary<NetPeer, IGameObject> _playerObjects = new();

        public PlayerManager(IGameState gameState, IUdpServer udpServer, IObjectApi objectApi, IObjectTypeManager objectTypeManager)
        {
            _gameState = gameState;
            _udpServer = udpServer;
            _objectApi = objectApi;
            _objectTypeManager = objectTypeManager;
        }

        public void OnPeerConnected(NetPeer peer)
        {
            var playerObjectType = _objectTypeManager.GetObjectType("/obj/player");
            if (playerObjectType == null)
            {
                Console.WriteLine("Error: /obj/player object type not found.");
                return;
            }

            var playerObject = _objectApi.CreateObject(playerObjectType.Id, 0, 0, 0);
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
