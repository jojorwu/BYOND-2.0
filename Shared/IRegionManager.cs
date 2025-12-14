using System.Collections.Generic;

namespace Shared
{
    public interface IRegionManager
    {
        void Initialize();
        IEnumerable<Region> GetRegions(int z);
        Task<IEnumerable<(Region, string, IEnumerable<IGameObject>)>> Tick();
    }
}
