using System.Collections.Generic;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;

namespace Core.Regions
{
    public interface IRegionActivationStrategy
    {
        HashSet<Region> GetActiveRegions();
        void SetRegionActive(int x, int y, int z, bool active);
    }
}
