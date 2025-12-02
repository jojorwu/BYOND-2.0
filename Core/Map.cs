using Robust.Shared.Maths;
using System;
using System.Collections.Generic;

namespace Core
{
    public class Map
    {
        private readonly Dictionary<int, Dictionary<Vector2i, Chunk>> _chunksByZ = new();

        public Map()
        {
        }

        public static (Vector2i ChunkCoords, Vector2i LocalCoords) GlobalToChunk(int x, int y)
        {
            var chunkX = (int)Math.Floor((double)x / Chunk.ChunkSize);
            var chunkY = (int)Math.Floor((double)y / Chunk.ChunkSize);
            var localX = x - chunkX * Chunk.ChunkSize;
            var localY = y - chunkY * Chunk.ChunkSize;
            return (new Vector2i(chunkX, chunkY), new Vector2i(localX, localY));
        }

        public Turf? GetTurf(int x, int y, int z)
        {
            var (chunkCoords, localCoords) = GlobalToChunk(x, y);

            if (_chunksByZ.TryGetValue(z, out var chunks) && chunks.TryGetValue(chunkCoords, out var chunk))
            {
                return chunk.GetTurf(localCoords.X, localCoords.Y);
            }

            return null;
        }

        public void SetTurf(int x, int y, int z, Turf turf)
        {
            var (chunkCoords, localCoords) = GlobalToChunk(x, y);

            if (!_chunksByZ.TryGetValue(z, out var chunks))
            {
                chunks = new Dictionary<Vector2i, Chunk>();
                _chunksByZ[z] = chunks;
            }

            if (!chunks.TryGetValue(chunkCoords, out var chunk))
            {
                chunk = new Chunk();
                chunks[chunkCoords] = chunk;
            }

            chunk.SetTurf(localCoords.X, localCoords.Y, turf);
        }

        public void SetChunk(int z, Vector2i chunkCoords, Chunk chunk)
        {
            if (!_chunksByZ.TryGetValue(z, out var chunks))
            {
                chunks = new Dictionary<Vector2i, Chunk>();
                _chunksByZ[z] = chunks;
            }

            chunks[chunkCoords] = chunk;
        }

        public IEnumerable<(Vector2i coords, Chunk chunk)> GetChunks(int z)
        {
            if (_chunksByZ.TryGetValue(z, out var chunks))
            {
                foreach (var (coords, chunk) in chunks)
                {
                    yield return (coords, chunk);
                }
            }
        }

        public IEnumerable<int> GetZLevels()
        {
            return _chunksByZ.Keys;
        }
    }
}
