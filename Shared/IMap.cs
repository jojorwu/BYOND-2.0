using Robust.Shared.Maths;
using System.Collections.Generic;

namespace Shared
{
    public interface IMap
    {
        int Width { get; }
        int Height { get; }
        int Depth { get; }
        ITurf? GetTurf(int x, int y, int z);
        void SetTurf(int x, int y, int z, ITurf turf);
        void SetChunk(int z, Vector2i chunkCoords, Chunk chunk);
        IEnumerable<(Vector2i coords, Chunk chunk)> GetChunks(int z);
        IEnumerable<int> GetZLevels();
        IEnumerable<IGameObject> GetAllGameObjects();
    }
}
