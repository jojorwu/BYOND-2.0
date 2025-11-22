using System.Collections.Generic;
using System.Linq;

namespace Core
{
    /// <summary>
    /// Represents a unique asset found on the map.
    /// </summary>
    public class MapAsset
    {
        /// <summary>
        /// Gets the name of the asset.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the type of the asset (e.g., "Turf" or "GameObject").
        /// </summary>
        public string AssetType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MapAsset"/> class.
        /// </summary>
        /// <param name="name">The name of the asset.</param>
        /// <param name="assetType">The type of the asset.</param>
        public MapAsset(string name, string assetType)
        {
            Name = name;
            AssetType = assetType;
        }
    }

    /// <summary>
    /// Provides logic for browsing assets on the game map.
    /// </summary>
    public class AssetBrowser
    {
        /// <summary>
        /// Scans the provided map and returns a list of all unique assets.
        /// </summary>
        /// <param name="map">The map to scan.</param>
        /// <returns>A list of unique assets found on the map.</returns>
        public List<MapAsset> GetAssets(Map map)
        {
            var assets = new List<MapAsset>();

            if (map == null)
            {
                return assets;
            }

            for (int z = 0; z < map.Depth; z++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    for (int x = 0; x < map.Width; x++)
                    {
                        var turf = map.GetTurf(x, y, z);
                        if (turf != null)
                        {
                            assets.Add(new MapAsset(turf.Id.ToString(), "Turf"));

                            foreach (var gameObject in turf.Contents)
                            {
                                assets.Add(new MapAsset(gameObject.Name, "GameObject"));
                            }
                        }
                    }
                }
            }

            return assets.GroupBy(a => new { a.Name, a.AssetType })
                         .Select(g => g.First())
                         .ToList();
        }
    }
}
