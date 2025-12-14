using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Maths;
using Shared;

namespace Core.Regions
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

        public bool TryGetRegion(int z, Vector2i coords, [NotNullWhen(true)] out Region? region)
        {
            if (_regionsByZ.TryGetValue(z, out var regions))
            {
                return regions.TryGetValue(coords, out region);
            }

            region = null;
            return false;
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
