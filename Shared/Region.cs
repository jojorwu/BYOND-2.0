using System.Collections.Generic;

namespace Shared
{
    public class Region
    {
        public const int RegionSize = 8; // in chunks
        private readonly List<Chunk> _chunks = new();

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
