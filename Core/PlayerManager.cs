using System.Collections.Generic;
using Robust.Shared.Maths;
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

        public void ForEachPlayerInRegion(Region region, Action<INetworkPeer> action)
        {
            _lock.EnterReadLock();
            try
            {
                foreach (var (peer, playerObject) in _players)
                {
                    var (chunkCoords, _) = Map.GlobalToChunk(playerObject.X, playerObject.Y);
                    var regionCoords = new Vector2i(
                        (int)Math.Floor((double)chunkCoords.X / Region.RegionSize),
                        (int)Math.Floor((double)chunkCoords.Y / Region.RegionSize)
                    );

                    if (region.Coords == regionCoords && region.Z == playerObject.Z)
                    {
                        action(peer);
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void ForEachPlayerObject(Action<IGameObject> action)
        {
            _lock.EnterReadLock();
            try
            {
                foreach (var playerObject in _players.Values)
                {
                    action(playerObject);
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
}
