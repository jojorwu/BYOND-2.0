using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;
using Robust.Shared.Maths;

namespace Shared.Services;
    public class InterestManager : IInterestManager
    {
        private readonly SpatialGrid _spatialGrid;
        private static readonly ThreadLocal<List<IGameObject>> _queryBuffer = new(() => new List<IGameObject>(65536));

        private struct PlayerInterestState
        {
            public long X;
            public long Y;
            public int Range;
        }

        private readonly ConcurrentDictionary<INetworkPeer, PlayerInterestState> _playerStates = new();

        public InterestManager(SpatialGrid spatialGrid)
        {
            _spatialGrid = spatialGrid;
        }

        public void UpdatePlayerInterest(INetworkPeer peer, long x, long y, int range)
        {
            _playerStates[peer] = new PlayerInterestState { X = x, Y = y, Range = range };
        }

        public IEnumerable<IGameObject> GetInterestedObjects(INetworkPeer peer)
        {
            if (_playerStates.TryGetValue(peer, out var state))
            {
                var box = new Box2l(state.X - state.Range, state.Y - state.Range, state.X + state.Range, state.Y + state.Range);

                // Optimized spatial query using non-allocating callback and thread-local buffer.
                // Safe because Serialize is called before the next query on this worker thread.
                var results = _queryBuffer.Value!;
                results.Clear();
                _spatialGrid.QueryBox(box, obj => results.Add(obj));
                return results;
            }
            return Enumerable.Empty<IGameObject>();
        }

        public void ClearPlayerInterest(INetworkPeer peer)
        {
            _playerStates.TryRemove(peer, out _);
        }
    }
