using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;
using Robust.Shared.Maths;

namespace Shared.Models
{
    public class Region
    {
        public Vector2i Coords { get; }
        public int Z { get; }
        private readonly List<Chunk> _chunks = new();

        public Region(Vector2i coords, int z)
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

        public IEnumerable<IGameObject> GetGameObjects()
        {
            foreach (var chunk in _chunks)
            {
                foreach (var turf in chunk.GetTurfs())
                {
                    if (turf == null)
                        continue;
                    foreach (var obj in turf.Contents)
                    {
                        yield return obj;
                    }
                }
            }
        }
    }
}
