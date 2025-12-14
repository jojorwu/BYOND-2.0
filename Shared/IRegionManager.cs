using System.Collections.Generic;

namespace Shared
{
    public interface IRegionManager
    {
        void Initialize();
        IEnumerable<Region> GetRegions(int z);
        HashSet<Region> GetActiveRegions();
        List<MergedRegion> MergeRegions(HashSet<Region> activeRegions);
        void SetRegionActive(int x, int y, int z, bool active);
    }
}
