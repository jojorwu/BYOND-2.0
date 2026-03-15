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

        public IEnumerable<IGameObject> GetInterestedObjects(INetworkPeer peer)
        {
            if (_playerStates.TryGetValue(peer, out var state))
            {
                var box = new Box2l(state.X - state.Range, state.Y - state.Range, state.X + state.Range, state.Y + state.Range);

                // Optimized spatial query using pre-allocated pooled list.
                // By returning a pooled list, we avoid IEnumerable enumerator allocations
                // and list resizing during high-frequency networking ticks.
                var results = _listPool.Rent();
                _spatialGrid.GetObjectsInBox(box, (IList<IGameObject>)results);
                return new PooledListWrapper(results);
            }
            return Enumerable.Empty<IGameObject>();
        }

        private struct PooledListWrapper : IEnumerable<IGameObject>, IEnumerator<IGameObject>
        {
            private List<IGameObject> _list;
            private int _index;

            public PooledListWrapper(List<IGameObject> list)
            {
                _list = list;
                _index = -1;
            }

            public IGameObject Current => _list[_index];
            object System.Collections.IEnumerator.Current => Current;

            public bool MoveNext() => ++_index < _list.Count;

            public void Reset() => _index = -1;

            public void Dispose()
            {
                if (_list != null)
                {
                    _list.Clear();
                    _listPool.Return(_list);
                    _list = null!;
                }
            }

            public IEnumerator<IGameObject> GetEnumerator() => this;
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this;
        }

        public void ClearPlayerInterest(INetworkPeer peer)
        {
            _playerStates.TryRemove(peer, out _);
        }
    }
