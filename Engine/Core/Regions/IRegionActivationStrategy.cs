using System.Collections.Generic;
using Shared;

namespace Core.Regions
{
    public interface IRegionActivationStrategy
    {
        HashSet<Region> GetActiveRegions();
        void SetRegionActive(long x, long y, long z, bool active);
    }
}
