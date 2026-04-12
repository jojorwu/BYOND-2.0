using Shared;
using Shared.Attributes;
using System.Linq;
using Robust.Shared.Maths;
using Microsoft.Extensions.Options;

namespace Core.Regions
{
    [EngineService(typeof(IRegionApi))]
    public class RegionApi : IRegionApi
    {
        private readonly IRegionManager _regionManager;
        private readonly IRegionActivationStrategy _regionActivationStrategy;
        private readonly ServerSettings _settings;

        public RegionApi(IRegionManager regionManager, IRegionActivationStrategy regionActivationStrategy, IOptions<ServerSettings> settings)
        {
            _regionManager = regionManager;
            _regionActivationStrategy = regionActivationStrategy;
            _settings = settings.Value;
        }

        public void SetRegionActive(long x, long y, long z, bool active)
        {
            _regionActivationStrategy.SetRegionActive(x, y, z, active);
        }

        public bool IsRegionActive(long x, long y, long z)
        {
            var (chunkCoords, _) = Map.GlobalToChunk(x, y);
            var regionCoords = (
                (long)Math.Floor((double)chunkCoords.X / _settings.Performance.RegionalProcessing.RegionSize),
                (long)Math.Floor((double)chunkCoords.Y / _settings.Performance.RegionalProcessing.RegionSize)
            );

            var region = _regionManager.GetRegions((int)z).FirstOrDefault(r => r.Coords == regionCoords);
            if (region == null)
                return false;

            return _regionActivationStrategy.GetActiveRegions().Contains(region);
        }
    }
}
