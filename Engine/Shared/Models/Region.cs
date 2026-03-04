using System.Collections.Generic;
using Robust.Shared.Maths;

namespace Shared;
    public class Region
    {
        public (long X, long Y) Coords { get; }
        public int Z { get; }
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

        public IEnumerable<Chunk> GetChunks()
        {
            return _chunks;
        }

        public void GetGameObjects(List<IGameObject> results)
        {
            foreach (var chunk in _chunks)
            {
                chunk.ForEachTurf((turf, x, y) =>
                {
                    if (turf != null)
                    {
                        results.AddRange(turf.Contents);
                    }
                });
            }
        }

        public IEnumerable<IGameObject> GetGameObjects()
        {
            var results = new List<IGameObject>();
            GetGameObjects(results);
            return results;
        }
    }
