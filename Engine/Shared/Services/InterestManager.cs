using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;
using Robust.Shared.Maths;

namespace Shared.Services;
    public class InterestManager : EngineService, IInterestManager
    {
        private readonly SpatialGrid _spatialGrid;
        private static readonly ThreadLocal<List<IGameObject>> _queryBuffer = new(() => new List<IGameObject>(65536));
        private static readonly SharedPool<List<IGameObject>> _listPool = new(() => new List<IGameObject>(1024));

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

        public InterestedObjectEnumerable GetInterestedObjects(INetworkPeer peer)
        {
            if (_playerStates.TryGetValue(peer, out var state))
            {
                var box = new Box2l(state.X - state.Range, state.Y - state.Range, state.X + state.Range, state.Y + state.Range);
                return new InterestedObjectEnumerable(_spatialGrid, box);
            }
            return default;
        }

        public struct InterestedObjectEnumerable : IEnumerable<IGameObject>
        {
            private readonly SpatialGrid _grid;
            private readonly Box2l _box;

            public bool IsDefault => _grid == null;

            public InterestedObjectEnumerable(SpatialGrid grid, Box2l box)
            {
                _grid = grid;
                _box = box;
            }

            public SpatialGrid.BoxEnumerator GetEnumerator() => _grid?.GetEnumerator(_box) ?? default;

            IEnumerator<IGameObject> IEnumerable<IGameObject>.GetEnumerator() => GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public void ClearPlayerInterest(INetworkPeer peer)
        {
            _playerStates.TryRemove(peer, out _);
        }
    }
