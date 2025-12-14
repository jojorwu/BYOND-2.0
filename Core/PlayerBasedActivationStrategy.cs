using System.Collections.Generic;
using Robust.Shared.Maths;
using Shared;

namespace Core
{
    public class PlayerBasedActivationStrategy : IRegionActivationStrategy
    {
        private readonly IPlayerManager _playerManager;
        private readonly ServerSettings _settings;
        private readonly Dictionary<int, Dictionary<Vector2i, Region>> _regionsByZ;
        private readonly ICollection<Region> _scriptActivatedRegions;

        public PlayerBasedActivationStrategy(IPlayerManager playerManager, ServerSettings settings, Dictionary<int, Dictionary<Vector2i, Region>> regionsByZ, ICollection<Region> scriptActivatedRegions)
        {
            _playerManager = playerManager;
            _settings = settings;
            _regionsByZ = regionsByZ;
            _scriptActivatedRegions = scriptActivatedRegions;
        }

        public HashSet<Region> GetActiveRegions()
        {
            var activeRegions = new HashSet<Region>(_scriptActivatedRegions);
            var playerCenterRegions = new HashSet<Region>();

            // 1. Collect unique regions where players are located
            _playerManager.ForEachPlayerObject(playerObject =>
            {
                var (chunkCoords, _) = Map.GlobalToChunk(playerObject.X, playerObject.Y);
                var regionCoords = new Vector2i(
                    (int)Math.Floor((double)chunkCoords.X / _settings.Performance.RegionalProcessing.RegionSize),
                    (int)Math.Floor((double)chunkCoords.Y / _settings.Performance.RegionalProcessing.RegionSize)
                );

                if (_regionsByZ.TryGetValue(playerObject.Z, out var regions) && regions.TryGetValue(regionCoords, out var region))
                {
                    playerCenterRegions.Add(region);
                }
            });

            // 2. Expand from the center regions
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
                            if (_regionsByZ.TryGetValue(currentZ, out var regions) && regions.TryGetValue(targetCoords, out var region))
                            {
                                activeRegions.Add(region);
                            }
                        }
                    }
                }
            }
            return activeRegions;
        }
    }
}
