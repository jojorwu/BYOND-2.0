using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly Dictionary<Region, float> _scriptActivatedRegions = new();
        private IRegionActivationStrategy _activationStrategy = null!;
        private readonly Stopwatch _stopwatch = new();

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
            _stopwatch.Start();
            foreach (var z in _map.GetZLevels())
            {
                if (!_regionsByZ.ContainsKey(z))
                {
                    _regionsByZ[z] = new Dictionary<Vector2i, Region>();
                }

                foreach (var (chunkCoords, chunk) in _map.GetChunks(z))
                {
                    var regionCoords = new Vector2i(
                        (int)Math.Floor((double)chunkCoords.X / _settings.Performance.RegionalProcessing.RegionSize),
                        (int)Math.Floor((double)chunkCoords.Y / _settings.Performance.RegionalProcessing.RegionSize)
                    );

                    if (!_regionsByZ[z].TryGetValue(regionCoords, out var region))
                    {
                        region = new Region(regionCoords, z);
                        _regionsByZ[z][regionCoords] = region;
                    }
                    region.AddChunk(chunk);
                }
            }
            _activationStrategy = new PlayerBasedActivationStrategy(_playerManager, _settings, _regionsByZ, _scriptActivatedRegions.Keys);
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
            CleanupExpiredScriptActivations();
            return _activationStrategy.GetActiveRegions();
        }

        private void CleanupExpiredScriptActivations()
        {
            var now = (float)_stopwatch.Elapsed.TotalSeconds;
            var timeout = _settings.Performance.RegionalProcessing.ScriptActiveRegionTimeout;
            var expiredRegions = new List<Region>();

            foreach (var (region, activationTime) in _scriptActivatedRegions)
            {
                if (now - activationTime > timeout)
                {
                    expiredRegions.Add(region);
                }
            }

            foreach (var region in expiredRegions)
            {
                _scriptActivatedRegions.Remove(region);
            }
        }

        public List<MergedRegion> MergeRegions(HashSet<Region> activeRegions)
        {
            if (!_settings.Performance.RegionalProcessing.EnableRegionMerging || activeRegions.Count < _settings.Performance.RegionalProcessing.MinRegionsToMerge)
            {
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

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (Math.Abs(dx) == Math.Abs(dy)) continue;

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

        public void SetRegionActive(int x, int y, int z, bool active)
        {
            var (chunkCoords, _) = Map.GlobalToChunk(x, y);
            var regionCoords = new Vector2i(
                (int)Math.Floor((double)chunkCoords.X / _settings.Performance.RegionalProcessing.RegionSize),
                (int)Math.Floor((double)chunkCoords.Y / _settings.Performance.RegionalProcessing.RegionSize)
            );

            if (_regionsByZ.TryGetValue(z, out var regions) && regions.TryGetValue(regionCoords, out var region))
            {
                if (active)
                    _scriptActivatedRegions[region] = (float)_stopwatch.Elapsed.TotalSeconds;
                else
                    _scriptActivatedRegions.Remove(region);
            }
        }
    }
}
