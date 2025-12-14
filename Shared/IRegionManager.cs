using System.Collections.Generic;

namespace Shared
{
    public interface IRegionManager
    {
        void Initialize();
        IEnumerable<Region> GetRegions(int z);
        IEnumerable<(Region, string)> Tick();
    }
}
