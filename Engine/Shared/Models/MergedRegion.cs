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

        public void GetGameObjects(IGameState gameState, List<IGameObject> results, int regionSizeInChunks = 8)
        {
            foreach (var region in Regions)
            {
                region.GetGameObjects(gameState, results, regionSizeInChunks);
            }
        }

        public IEnumerable<IGameObject> GetGameObjects(IGameState gameState, int regionSizeInChunks = 8)
        {
            var results = new List<IGameObject>();
            GetGameObjects(gameState, results, regionSizeInChunks);
            return results;
        }
    }
