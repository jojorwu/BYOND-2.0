using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using Robust.Shared.Maths;
using System;
using System.Collections.Generic;

namespace Shared.Models
{
    public class Map : IMap
    {
        private readonly Dictionary<int, Dictionary<Vector2i, Chunk>> _chunksByZ = new();

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

        public void SetTurf(int x, int y, int z, ITurf turf)
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

        public IEnumerable<IGameObject> GetAllGameObjects()
        {
            foreach (var z in _chunksByZ.Keys)
            {
                foreach (var chunk in _chunksByZ[z].Values)
                {
                    foreach (var turf in chunk.GetTurfs())
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
