using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;
using System.Linq;

namespace Shared.Models
{
    public class MergedRegion
    {
        public List<Region> Regions { get; }

        public MergedRegion(List<Region> regions)
        {
            Regions = regions;
        }

        public IEnumerable<IGameObject> GetGameObjects()
        {
            return Regions.SelectMany(r => r.GetGameObjects());
        }
    }
}
