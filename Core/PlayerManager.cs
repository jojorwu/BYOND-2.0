using System.Collections.Generic;
using Shared;

namespace Core
{
    public class PlayerManager : IPlayerManager
    {
        private readonly Dictionary<INetworkPeer, IGameObject> _players = new();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private readonly IObjectApi _objectApi;
        private readonly IObjectTypeManager _objectTypeManager;
        private readonly ServerSettings _settings;

        public PlayerManager(IObjectApi objectApi, IObjectTypeManager objectTypeManager, ServerSettings settings)
        {
            _objectApi = objectApi;
            _objectTypeManager = objectTypeManager;
            _settings = settings;
        }

        public void AddPlayer(INetworkPeer peer)
        {
            _lock.EnterWriteLock();
            try
            {
                var playerObjectType = _objectTypeManager.GetObjectType(_settings.PlayerObjectTypePath);
                if (playerObjectType != null)
                {
                    var playerObject = _objectApi.CreateObject(playerObjectType.Id, 0, 0, 0);
                    if (playerObject != null)
                        _players[peer] = playerObject;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void RemovePlayer(INetworkPeer peer)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_players.TryGetValue(peer, out var playerObject))
                {
                    _objectApi.DestroyObject(playerObject.Id);
                    _players.Remove(peer);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public IEnumerable<INetworkPeer> GetPlayersInRegion(Region region)
        {
            _lock.EnterReadLock();
            try
            {
                var regionObjects = new HashSet<IGameObject>(region.GetGameObjects());
                var playersInRegion = new List<INetworkPeer>();
                foreach (var (peer, playerObject) in _players)
                {
                    if (regionObjects.Contains(playerObject))
                    {
                        playersInRegion.Add(peer);
                    }
                }
                return playersInRegion;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public IEnumerable<IGameObject> GetAllPlayerObjects()
        {
            _lock.EnterReadLock();
            try
            {
                return new List<IGameObject>(_players.Values);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
}
