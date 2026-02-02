using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
namespace Shared.Models
{
    public class Chunk
    {
        public const int ChunkSize = 16;

        private readonly ITurf[,] _turfs;

        public Chunk()
        {
            _turfs = new ITurf[ChunkSize, ChunkSize];
        }

        public ITurf? GetTurf(int x, int y)
        {
            if (x < 0 || x >= ChunkSize || y < 0 || y >= ChunkSize)
            {
                return null;
            }
            return _turfs[x, y];
        }

        public void SetTurf(int x, int y, ITurf turf)
        {
            if (x >= 0 && x < ChunkSize && y >= 0 && y < ChunkSize)
            {
                _turfs[x, y] = turf;
            }
        }

        public IEnumerable<ITurf> GetTurfs()
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                for (int x = 0; x < ChunkSize; x++)
                {
                    yield return _turfs[x, y];
                }
            }
        }
    }
}
