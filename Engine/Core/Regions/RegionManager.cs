using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Maths;
using Shared;
using Microsoft.Extensions.Options;

namespace Core.Regions
{
    public class RegionManager : IRegionManager
    {
        private readonly IMap _map;
        private readonly ServerSettings _settings;
        private readonly Dictionary<int, Dictionary<Vector2i, Region>> _regionsByZ = new();

        public RegionManager(IMap map, IOptions<ServerSettings> settings)
        {
            _map = map;
            _settings = settings.Value;
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
    }
}
