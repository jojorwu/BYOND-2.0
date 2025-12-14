using System.Collections.Generic;
using Shared;

namespace Core
{
    public interface IRegionActivationStrategy
    {
        HashSet<Region> GetActiveRegions();
    }
}
