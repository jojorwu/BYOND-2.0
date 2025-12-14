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

        public async Task<IEnumerable<(Region, string, IEnumerable<IGameObject>)>> Tick()
        {
            var activeRegions = GetActiveRegions();
            var snapshots = new System.Collections.Concurrent.ConcurrentBag<(Region, string, IEnumerable<IGameObject>)>();
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = _settings.Performance.RegionalProcessing.MaxThreads > 0
                    ? _settings.Performance.RegionalProcessing.MaxThreads
                    : -1
            };

            await Parallel.ForEachAsync(activeRegions, options, (region, token) =>
            {
                var gameObjects = region.GetGameObjects().ToList();
                snapshots.Add((region, _gameState.GetSnapshot(region), gameObjects));
                return ValueTask.CompletedTask;
            });
            return snapshots;
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

        private HashSet<Region> GetActiveRegions()
        {
            var activeRegions = new HashSet<Region>(_scriptActivatedRegions);
            _playerManager.ForEachPlayerObject(playerObject =>
            {
                var (chunkCoords, _) = Map.GlobalToChunk(playerObject.X, playerObject.Y);
                var regionCoords = new Vector2i(
                    (int)Math.Floor((double)chunkCoords.X / Region.RegionSize),
                    (int)Math.Floor((double)chunkCoords.Y / Region.RegionSize)
                );

                var z = playerObject.Z;
                var zRange = _settings.Performance.RegionalProcessing.ZActivationRange;

                for(int zOffset = -zRange; zOffset <= zRange; zOffset++)
                {
                    var currentZ = z + zOffset;
                    for (int x = -_settings.Performance.RegionalProcessing.ActivationRange; x <= _settings.Performance.RegionalProcessing.ActivationRange; x++)
                    {
                        for (int y = -_settings.Performance.RegionalProcessing.ActivationRange; y <= _settings.Performance.RegionalProcessing.ActivationRange; y++)
                        {
                            if (_regionsByZ.TryGetValue(currentZ, out var regions) && regions.TryGetValue(new Vector2i(regionCoords.X + x, regionCoords.Y + y), out var region))
                            {
                                activeRegions.Add(region);
                            }
                        }
                    }
                }
            });
            return activeRegions;
        }
    }
}
