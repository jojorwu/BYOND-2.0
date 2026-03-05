using System.Collections.Generic;
using Robust.Shared.Maths;

namespace Shared;
    public class Region
    {
        public (long X, long Y) Coords { get; }
        public int Z { get; }
        public long Version { get; private set; }
        private readonly List<Chunk> _chunks = new();

        public Region((long X, long Y) coords, int z)
        {
            Coords = coords;
            Z = z;
        }

        public void AddChunk(Chunk chunk)
        {
            _chunks.Add(chunk);
        }

        public void UpdateVersion()
        {
            long v = 0;
            foreach (var chunk in _chunks) v += chunk.Version;
            Version = v;
        }

        public IEnumerable<Chunk> GetChunks()
        {
            return _chunks;
        }

        public void GetGameObjects(IGameState gameState, List<IGameObject> results, int regionSizeInChunks = 8)
        {
            const int ChunkSize = 32;

            long startX = Coords.X * regionSizeInChunks * ChunkSize;
            long startY = Coords.Y * regionSizeInChunks * ChunkSize;
            long endX = (Coords.X + 1) * regionSizeInChunks * ChunkSize - 1;
            long endY = (Coords.Y + 1) * regionSizeInChunks * ChunkSize - 1;

            var box = new Box2l(startX, startY, endX, endY);

            // Fast non-allocating query path
            gameState.SpatialGrid.QueryBoxZ(box, Z, results);
        }

        public IEnumerable<IGameObject> GetGameObjects(IGameState gameState, int regionSizeInChunks = 8)
        {
            var results = new List<IGameObject>();
            GetGameObjects(gameState, results, regionSizeInChunks);
            return results;
        }
    }
