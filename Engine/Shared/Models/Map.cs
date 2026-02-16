using Robust.Shared.Maths;
using System;
using System.Collections.Generic;

namespace Shared
{
    public class Map : IMap
    {
        private const int MaxCoordinate = 100000;
        private const int MaxChunksPerZ = 10000;
        private const int MaxZLevels = 100;
        private readonly Dictionary<int, Dictionary<Vector2i, Chunk>> _chunksByZ = new();
        private readonly object _mapLock = new();

        public Map()
        {
        }

        public static (Vector2i chunkCoords, Vector2i localCoords) GlobalToChunk(int x, int y)
        {
            var chunkX = (int)Math.Floor((double)x / Chunk.ChunkSize);
            var chunkY = (int)Math.Floor((double)y / Chunk.ChunkSize);
            var localX = x - chunkX * Chunk.ChunkSize;
            var localY = y - chunkY * Chunk.ChunkSize;
            return (new Vector2i(chunkX, chunkY), new Vector2i(localX, localY));
        }

        public ITurf? GetTurf(int x, int y, int z)
        {
            var (chunkCoords, localCoords) = GlobalToChunk(x, y);

            if (_chunksByZ.TryGetValue(z, out var chunks) && chunks.TryGetValue(chunkCoords, out var chunk))
            {
                return chunk.GetTurf(localCoords.X, localCoords.Y);
            }

            return null;
        }

        public int GetTurfTypeId(int x, int y, int z)
        {
            var (chunkCoords, localCoords) = GlobalToChunk(x, y);

            if (_chunksByZ.TryGetValue(z, out var chunks) && chunks.TryGetValue(chunkCoords, out var chunk))
            {
                return chunk.GetTurfTypeId(localCoords.X, localCoords.Y);
            }

            return 0;
        }

        public void SetTurf(int x, int y, int z, ITurf turf)
        {
            if (Math.Abs(x) > MaxCoordinate || Math.Abs(y) > MaxCoordinate || z < 0 || z >= MaxZLevels)
                return;

            var (chunkCoords, localCoords) = GlobalToChunk(x, y);

            Chunk chunk = GetOrCreateChunk(z, chunkCoords);
            chunk.SetTurf(localCoords.X, localCoords.Y, turf);
        }

        public void SetTurfType(int x, int y, int z, int typeId)
        {
            if (Math.Abs(x) > MaxCoordinate || Math.Abs(y) > MaxCoordinate || z < 0 || z >= MaxZLevels)
                return;

            var (chunkCoords, localCoords) = GlobalToChunk(x, y);

            Chunk chunk = GetOrCreateChunk(z, chunkCoords);
            chunk.SetTurfType(localCoords.X, localCoords.Y, typeId);
        }

        private Chunk GetOrCreateChunk(int z, Vector2i chunkCoords)
        {
            lock (_mapLock)
            {
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

                return chunk;
            }
        }

        public void SetChunk(int z, Vector2i chunkCoords, Chunk chunk)
        {
            if (z < 0 || z >= MaxZLevels) return;
            if (Math.Abs(chunkCoords.X) > MaxCoordinate / Chunk.ChunkSize || Math.Abs(chunkCoords.Y) > MaxCoordinate / Chunk.ChunkSize)
                return;

            if (!_chunksByZ.TryGetValue(z, out var chunks))
            {
                if (_chunksByZ.Count >= MaxZLevels) return;
                chunks = new Dictionary<Vector2i, Chunk>();
                _chunksByZ[z] = chunks;
            }

            if (!chunks.ContainsKey(chunkCoords) && chunks.Count >= MaxChunksPerZ)
                return;

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

        public IEnumerable<IGameObject> GetAllGameObjects()
        {
            foreach (var z in _chunksByZ.Keys)
            {
                foreach (var chunk in _chunksByZ[z].Values)
                {
                    foreach (var turf in chunk.GetTurfObjects())
                    {
                        foreach (var obj in turf.Contents)
                        {
                            yield return obj;
                        }
                    }
                }
            }
        }

        public void AddObjectToTurf(GameObject gameObject)
        {
            var turf = GetTurf(gameObject.X, gameObject.Y, gameObject.Z);
            turf?.AddContent(gameObject);
        }

        public void RemoveObjectFromTurf(GameObject gameObject)
        {
            var turf = GetTurf(gameObject.X, gameObject.Y, gameObject.Z);
            turf?.RemoveContent(gameObject);
        }
    }
}
