using Shared;
using System.Linq;
using Robust.Shared.Maths;

namespace Core.Regions
{
    public class RegionApi : IRegionApi
    {
        private readonly IRegionManager _regionManager;
        private readonly IRegionActivationStrategy _regionActivationStrategy;
        private readonly ServerSettings _settings;

        public RegionApi(IRegionManager regionManager, IRegionActivationStrategy regionActivationStrategy, ServerSettings settings)
        {
            _regionManager = regionManager;
            _regionActivationStrategy = regionActivationStrategy;
            _settings = settings;
        }

        public void SetRegionActive(int x, int y, int z, bool active)
        {
            _regionActivationStrategy.SetRegionActive(x, y, z, active);
        }

        public bool IsRegionActive(int x, int y, int z)
        {
            var (chunkCoords, _) = Map.GlobalToChunk(x, y);
            var regionCoords = new Vector2i(
                (int)Math.Floor((double)chunkCoords.X / _settings.Performance.RegionalProcessing.RegionSize),
                (int)Math.Floor((double)chunkCoords.Y / _settings.Performance.RegionalProcessing.RegionSize)
            );

            var region = _regionManager.GetRegions(z).FirstOrDefault(r => r.Coords == regionCoords);
            if (region == null)
                return false;

            return _regionActivationStrategy.GetActiveRegions().Contains(region);
        }
    }
}
