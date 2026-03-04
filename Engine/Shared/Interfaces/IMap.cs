using Robust.Shared.Maths;
using System.Collections.Generic;

namespace Shared;
    public interface IMap
    {
        ITurf? GetTurf(long x, long y, long z);
        int GetTurfTypeId(long x, long y, long z);
        void SetTurf(long x, long y, long z, ITurf turf);
        void SetTurfType(long x, long y, long z, int typeId);
        void SetChunk(int z, (long X, long Y) chunkCoords, Chunk chunk);
        IEnumerable<((long X, long Y) coords, Chunk chunk)> GetChunks(int z);
        IEnumerable<int> GetZLevels();
        IEnumerable<IGameObject> GetAllGameObjects();
        void AddObjectToTurf(GameObject gameObject);
        void RemoveObjectFromTurf(GameObject gameObject);
    }
