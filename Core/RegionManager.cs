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
        private readonly IPlayerManager _playerManager;
        private readonly ServerSettings _settings;
        private readonly Dictionary<int, Dictionary<Vector2i, Region>> _regionsByZ = new();
        private readonly HashSet<Region> _scriptActivatedRegions = new();
        private IRegionActivationStrategy _activationStrategy = null!;

        public RegionManager(IMap map, IScriptHost scriptHost, IGameState gameState, IPlayerManager playerManager, ServerSettings settings)
        {
            _map = map;
            _scriptHost = scriptHost;
            _gameState = gameState;
            _playerManager = playerManager;
            _settings = settings;
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
                        region = new Region(regionCoords, z);
                        _regionsByZ[z][regionCoords] = region;
                    }
                    region.AddChunk(chunk);
                }
            }
            _activationStrategy = new PlayerBasedActivationStrategy(_playerManager, _settings, _regionsByZ, _scriptActivatedRegions);
        }

        public IEnumerable<Region> GetRegions(int z)
        {
            if (_regionsByZ.TryGetValue(z, out var regions))
            {
                return regions.Values;
            }
            return new List<Region>();
        }

        public HashSet<Region> GetActiveRegions()
        {
            return _activationStrategy.GetActiveRegions();
        }

        public List<MergedRegion> MergeRegions(HashSet<Region> activeRegions)
        {
            if (!_settings.Performance.RegionalProcessing.EnableRegionMerging || activeRegions.Count < _settings.Performance.RegionalProcessing.MinRegionsToMerge)
            {
                // If merging is disabled or not enough regions, return each region as a single-region group.
                return activeRegions.Select(r => new MergedRegion(new List<Region> { r })).ToList();
            }

            var mergedRegions = new List<MergedRegion>();
            var visited = new HashSet<Region>();

            var regionsByZ = activeRegions.GroupBy(r => r.Z)
                .ToDictionary(g => g.Key, g => g.ToHashSet());

            foreach (var z in regionsByZ.Keys)
            {
                foreach (var region in regionsByZ[z])
                {
                    if (visited.Contains(region))
                        continue;

                    var group = new List<Region>();
                    var queue = new Queue<Region>();

                    queue.Enqueue(region);
                    visited.Add(region);

                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        group.Add(current);

                        // Check neighbors
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (Math.Abs(dx) == Math.Abs(dy)) continue; // Skip diagonals and self

                                var neighborCoords = new Vector2i(current.Coords.X + dx, current.Coords.Y + dy);
                                if (_regionsByZ[z].TryGetValue(neighborCoords, out var neighbor) && regionsByZ[z].Contains(neighbor) && !visited.Contains(neighbor))
                                {
                                    visited.Add(neighbor);
                                    queue.Enqueue(neighbor);
                                }
                            }
                        }
                    }
                    mergedRegions.Add(new MergedRegion(group));
                }
            }
            return mergedRegions;
        }

        private bool AreAdjacent(Region a, Region b)
        {
            if (a.Z != b.Z)
                return false;

            var dx = Math.Abs(a.Coords.X - b.Coords.X);
            var dy = Math.Abs(a.Coords.Y - b.Coords.Y);

            return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
        }

        public void SetRegionActive(int x, int y, int z, bool active)
        {
            var (chunkCoords, _) = Map.GlobalToChunk(x, y);
            var regionCoords = new Vector2i(
                (int)Math.Floor((double)chunkCoords.X / Region.RegionSize),
                (int)Math.Floor((double)chunkCoords.Y / Region.RegionSize)
            );

            if (_regionsByZ.TryGetValue(z, out var regions) && regions.TryGetValue(regionCoords, out var region))
            {
                if (active)
                    _scriptActivatedRegions.Add(region);
                else
                    _scriptActivatedRegions.Remove(region);
            }
        }
    }
}
