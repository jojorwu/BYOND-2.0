namespace Core
{
    public class Chunk
    {
        public const int ChunkSize = 16;

        private readonly Turf[,] _turfs;

        public Chunk()
        {
            _turfs = new Turf[ChunkSize, ChunkSize];
        }

        public Turf? GetTurf(int x, int y)
        {
            if (x < 0 || x >= ChunkSize || y < 0 || y >= ChunkSize)
            {
                return null;
            }
            return _turfs[x, y];
        }

        public void SetTurf(int x, int y, Turf turf)
        {
            if (x >= 0 && x < ChunkSize && y >= 0 && y < ChunkSize)
            {
                _turfs[x, y] = turf;
            }
        }
    }
}
