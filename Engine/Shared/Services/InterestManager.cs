using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Shared.Interfaces;
using Robust.Shared.Maths;

namespace Shared.Services;
    public class InterestManager : EngineService, IInterestManager
    {
        private readonly SpatialGrid _spatialGrid;
        private readonly IDiagnosticBus _diagnosticBus;
        private static readonly ThreadLocal<List<IGameObject>> _queryBuffer = new(() => new List<IGameObject>(65536));
        private static readonly SharedPool<List<IGameObject>> _listPool = new(() => new List<IGameObject>(1024));
        private long _totalInterestUpdates;
        private long _totalQueryVolume;

        private struct PlayerInterestState
        {
            public Vector3l Position;
            public int Range;
        }

        private readonly ConcurrentDictionary<INetworkPeer, PlayerInterestState> _playerStates = new();

        public InterestManager(SpatialGrid spatialGrid, IDiagnosticBus diagnosticBus)
        {
            _spatialGrid = spatialGrid;
            _diagnosticBus = diagnosticBus;
        }

        public void UpdatePlayerInterest(INetworkPeer peer, long x, long y, int range)
        {
            Interlocked.Increment(ref _totalInterestUpdates);
            _playerStates[peer] = new PlayerInterestState { Position = new Vector3l(x, y, 0), Range = range };
        }

        public InterestedObjectEnumerable GetInterestedObjects(INetworkPeer peer)
        {
            Interlocked.Increment(ref _totalQueryVolume);
            if (_playerStates.TryGetValue(peer, out var state))
            {
                var box = new Box3l(state.Position.X - state.Range, state.Position.Y - state.Range, -100, state.Position.X + state.Range, state.Position.Y + state.Range, 100);
                return new InterestedObjectEnumerable(_spatialGrid, box);
            }
            return default;
        }

        public struct InterestedObjectEnumerable : IEnumerable<IGameObject>
        {
            private readonly SpatialGrid _grid;
            private readonly Box3l _box;

            public bool IsDefault => _grid == null;

            public InterestedObjectEnumerable(SpatialGrid grid, Box3l box)
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

        public override Dictionary<string, object> GetDiagnosticInfo()
        {
            var info = base.GetDiagnosticInfo();
            info["ActivePlayerInterests"] = _playerStates.Count;
            info["TotalInterestUpdates"] = Interlocked.Read(ref _totalInterestUpdates);
            info["TotalQueryVolume"] = Interlocked.Read(ref _totalQueryVolume);
            return info;
        }
    }
