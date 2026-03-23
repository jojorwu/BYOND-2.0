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
            public long Z;
            public int Range;
        }

        private readonly ConcurrentDictionary<INetworkPeer, PlayerInterestState> _playerStates = new();

        public InterestManager(SpatialGrid spatialGrid)
        {
            _spatialGrid = spatialGrid;
        }

        public void UpdatePlayerInterest(INetworkPeer peer, long x, long y, long z, int range)
        {
            _playerStates[peer] = new PlayerInterestState { X = x, Y = y, Z = z, Range = range };
        }

        public InterestedObjectEnumerable GetInterestedObjects(INetworkPeer peer)
        {
            if (_playerStates.TryGetValue(peer, out var state))
            {
                var box = new Box2l(state.X - state.Range, state.Y - state.Range, state.X + state.Range, state.Y + state.Range);
                return new InterestedObjectEnumerable(_spatialGrid, box, state.Z);
            }
            return default;
        }

        public struct InterestedObjectEnumerable : IEnumerable<IGameObject>
        {
            private readonly SpatialGrid _grid;
            private readonly Box2l _box;
            private readonly long _z;

            public bool IsDefault => _grid == null;

            public InterestedObjectEnumerable(SpatialGrid grid, Box2l box, long z)
            {
                _grid = grid;
                _box = box;
                _z = z;
            }

            public SpatialGrid.BoxEnumerator GetEnumerator() => _grid?.GetEnumerator(_box, _z) ?? default;

            IEnumerator<IGameObject> IEnumerable<IGameObject>.GetEnumerator() => GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public void ClearPlayerInterest(INetworkPeer peer)
        {
            _playerStates.TryRemove(peer, out _);
        }
    }
