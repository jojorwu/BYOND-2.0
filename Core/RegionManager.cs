using System.Collections.Generic;
using Robust.Shared.Maths;
using Shared;

namespace Core
{
    public class RegionManager : IRegionManager
    {
        private readonly IMap _map;
        private readonly IScriptHost _scriptHost;
        private readonly IGameState _gameState;
        private readonly Dictionary<int, Dictionary<Vector2i, Region>> _regionsByZ = new();

        public RegionManager(IMap map, IScriptHost scriptHost, IGameState gameState)
        {
            _map = map;
            _scriptHost = scriptHost;
            _gameState = gameState;
        }

        public void Initialize()
        {
            foreach (var z in _map.GetZLevels())
            {
                if (!_regionsByZ.ContainsKey(z))
                {
                    _regionsByZ[z] = new Dictionary<Vector2i, Region>();
                }

                foreach (var (chunkCoords, chunk) in _map.GetChunks(z))
                {
                    var regionCoords = new Vector2i(
                        (int)Math.Floor((double)chunkCoords.X / Region.RegionSize),
                        (int)Math.Floor((double)chunkCoords.Y / Region.RegionSize)
                    );

                    if (!_regionsByZ[z].TryGetValue(regionCoords, out var region))
                    {
                        region = new Region();
                        _regionsByZ[z][regionCoords] = region;
                    }
                    region.AddChunk(chunk);
                }
            }
        }

        public IEnumerable<Region> GetRegions(int z)
        {
            if (_regionsByZ.TryGetValue(z, out var regions))
            {
                return regions.Values;
            }
            return new List<Region>();
        }

        public IEnumerable<(Region, string)> Tick()
        {
            var snapshots = new List<(Region, string)>();
            foreach (var z in _regionsByZ.Keys)
            {
                foreach (var region in _regionsByZ[z].Values)
                {
                    _scriptHost.Tick(region.GetGameObjects());
                    snapshots.Add((region, _gameState.GetSnapshot(region)));
                }
            }
            return snapshots;
        }
    }
}
