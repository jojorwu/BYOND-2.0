using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared
{
    public interface IMapApi
    {
        IMap? GetMap();
        ITurf? GetTurf(int x, int y, int z);
        void SetTurf(int x, int y, int z, int turfId);
        Task<IMap?> LoadMapAsync(string filePath);
        void SetMap(IMap map);
        Task SaveMapAsync(string filePath);

        /// <summary>
        /// Gets all game objects within a specified range of a central point.
        /// </summary>
        IEnumerable<IGameObject> GetObjectsInRange(int x, int y, int z, int range);

        /// <summary>
        /// Gets all game objects of a specific type (and its subtypes) within a specified range.
        /// </summary>
        IEnumerable<IGameObject> GetObjectsInRange(int x, int y, int z, int range, string typePath);

        /// <summary>
        /// Gets all game objects within a specified rectangular area.
        /// </summary>
        IEnumerable<IGameObject> GetObjectsInArea(int x1, int y1, int x2, int y2, int z);

        /// <summary>
        /// Gets all game objects of a specific type (and its subtypes) within a specified rectangular area.
        /// </summary>
        IEnumerable<IGameObject> GetObjectsInArea(int x1, int y1, int x2, int y2, int z, string typePath);
    }
}
