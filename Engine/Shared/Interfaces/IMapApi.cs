using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared;
    public interface IMapApi
    {
        IMap? GetMap();
        ITurf? GetTurf(long x, long y, long z);
        void SetTurf(long x, long y, long z, int turfId);
        Task<IMap?> LoadMapAsync(string filePath);
        void SetMap(IMap map);
        Task SaveMapAsync(string filePath);

        /// <summary>
        /// Gets all game objects within a specified range of a central point.
        /// </summary>
        IEnumerable<IGameObject> GetObjectsInRange(long x, long y, long z, int range);

        /// <summary>
        /// Gets all game objects of a specific type (and its subtypes) within a specified range.
        /// </summary>
        IEnumerable<IGameObject> GetObjectsInRange(long x, long y, long z, int range, string typePath);

        /// <summary>
        /// Gets all game objects within a specified rectangular area.
        /// </summary>
        IEnumerable<IGameObject> GetObjectsInArea(long x1, long y1, long x2, long y2, long z);

        /// <summary>
        /// Gets all game objects of a specific type (and its subtypes) within a specified rectangular area.
        /// </summary>
        IEnumerable<IGameObject> GetObjectsInArea(long x1, long y1, long x2, long y2, long z, string typePath);
    }
