using System.Collections.Generic;
using System.Diagnostics;
using Robust.Shared.Maths;
using Shared;
using Microsoft.Extensions.Options;

namespace Core.Regions
{
    public class PlayerBasedActivationStrategy : IRegionActivationStrategy
    {
        private readonly IPlayerManager _playerManager;
        private readonly IRegionManager _regionManager;
        private readonly ServerSettings _settings;
        private readonly Dictionary<Region, float> _scriptActivatedRegions = new();
        private readonly Stopwatch _stopwatch = new();

        public PlayerBasedActivationStrategy(IPlayerManager playerManager, IRegionManager regionManager, IOptions<ServerSettings> settings)
        {
            _playerManager = playerManager;
            _regionManager = regionManager;
            _settings = settings.Value;
            _stopwatch.Start();
        }

        public HashSet<Region> GetActiveRegions()
        {
            CleanupExpiredScriptActivations();

            var activeRegions = new HashSet<Region>(_scriptActivatedRegions.Keys);
            var playerCenterRegions = new HashSet<Region>();

            _playerManager.ForEachPlayerObject(playerObject =>
            {
                var (chunkCoords, _) = Map.GlobalToChunk(playerObject.X, playerObject.Y);
                var regionCoords = new Vector2i(
                    (int)Math.Floor((double)chunkCoords.X / _settings.Performance.RegionalProcessing.RegionSize),
                    (int)Math.Floor((double)chunkCoords.Y / _settings.Performance.RegionalProcessing.RegionSize)
                );

                if (_regionManager.TryGetRegion(playerObject.Z, regionCoords, out var region))
                {
                    playerCenterRegions.Add(region);
                }
            });

            foreach (var centerRegion in playerCenterRegions)
            {
                var zRange = _settings.Performance.RegionalProcessing.ZActivationRange;
                for (int zOffset = -zRange; zOffset <= zRange; zOffset++)
                {
                    var currentZ = centerRegion.Z + zOffset;
                    var range = _settings.Performance.RegionalProcessing.ActivationRange;
                    for (int x = -range; x <= range; x++)
                    {
                        for (int y = -range; y <= range; y++)
                        {
                            var targetCoords = new Vector2i(centerRegion.Coords.X + x, centerRegion.Coords.Y + y);
                            if (_regionManager.TryGetRegion(currentZ, targetCoords, out var region))
                            {
                                activeRegions.Add(region);
                            }
                        }
                    }
                }
            }
            return activeRegions;
        }

        public void SetRegionActive(int x, int y, int z, bool active)
        {
            var (chunkCoords, _) = Map.GlobalToChunk(x, y);
            var regionCoords = new Vector2i(
                (int)Math.Floor((double)chunkCoords.X / _settings.Performance.RegionalProcessing.RegionSize),
                (int)Math.Floor((double)chunkCoords.Y / _settings.Performance.RegionalProcessing.RegionSize)
            );

            if (_regionManager.TryGetRegion(z, regionCoords, out var region))
            {
                if (active)
                    _scriptActivatedRegions[region] = (float)_stopwatch.Elapsed.TotalSeconds;
                else
                    _scriptActivatedRegions.Remove(region);
            }
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
    }
}
