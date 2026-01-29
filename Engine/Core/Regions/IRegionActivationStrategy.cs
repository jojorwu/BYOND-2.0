using System.Collections.Generic;
using Shared;

namespace Core.Regions
{
    public interface IRegionActivationStrategy
    {
        HashSet<Region> GetActiveRegions();
        void SetRegionActive(int x, int y, int z, bool active);
    }
}
