using Shared;

namespace Core
{
    public class RegionApi : IRegionApi
    {
        private readonly IRegionManager _regionManager;

        public RegionApi(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }

        public void SetRegionActive(int x, int y, int z, bool active)
        {
            _regionManager.SetRegionActive(x, y, z, active);
        }
    }
}
