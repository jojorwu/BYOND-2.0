using System.Collections.Generic;
using System.Linq;

namespace Shared;
    public class MergedRegion
    {
        public List<Region> Regions { get; }

        public MergedRegion(List<Region> regions)
        {
            Regions = regions;
        }

        public void GetGameObjects(List<IGameObject> results)
        {
            foreach (var region in Regions)
            {
                region.GetGameObjects(results);
            }
        }

        public IEnumerable<IGameObject> GetGameObjects()
        {
            var results = new List<IGameObject>();
            GetGameObjects(results);
            return results;
        }
    }
