using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;
using Robust.Shared.Maths;

namespace Shared.Services
{
    public class InterestManager : IInterestManager
    {
        private readonly SpatialGrid _spatialGrid;

        private struct PlayerInterestState
        {
            public int X;
            public int Y;
            public int Range;
        }

        private readonly ConcurrentDictionary<INetworkPeer, PlayerInterestState> _playerStates = new();

        public InterestManager(SpatialGrid spatialGrid)
        {
            _spatialGrid = spatialGrid;
        }

        public void UpdatePlayerInterest(INetworkPeer peer, int x, int y, int range)
        {
            _playerStates[peer] = new PlayerInterestState { X = x, Y = y, Range = range };
        }

        public IEnumerable<IGameObject> GetInterestedObjects(INetworkPeer peer)
        {
            if (_playerStates.TryGetValue(peer, out var state))
            {
                var box = new Box2i(state.X - state.Range, state.Y - state.Range, state.X + state.Range, state.Y + state.Range);
                return _spatialGrid.GetObjectsInBox(box);
            }
            return Enumerable.Empty<IGameObject>();
        }

        public void ClearPlayerInterest(INetworkPeer peer)
        {
            _playerStates.TryRemove(peer, out _);
        }
    }
}
