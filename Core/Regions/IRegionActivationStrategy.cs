using System.Collections.Generic;
using Shared;

namespace Core.Regions
{
    public interface IRegionActivationStrategy
    {
        HashSet<Region> GetActiveRegions();
    }
}
