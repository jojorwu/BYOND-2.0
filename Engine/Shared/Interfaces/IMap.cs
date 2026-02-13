using Robust.Shared.Maths;
using System.Collections.Generic;

namespace Shared
{
    public interface IMap
    {
        ITurf? GetTurf(int x, int y, int z);
        int GetTurfTypeId(int x, int y, int z);
        void SetTurf(int x, int y, int z, ITurf turf);
        void SetTurfType(int x, int y, int z, int typeId);
        void SetChunk(int z, Vector2i chunkCoords, Chunk chunk);
        IEnumerable<(Vector2i coords, Chunk chunk)> GetChunks(int z);
        IEnumerable<int> GetZLevels();
        IEnumerable<IGameObject> GetAllGameObjects();
        void AddObjectToTurf(GameObject gameObject);
        void RemoveObjectFromTurf(GameObject gameObject);
    }
}
