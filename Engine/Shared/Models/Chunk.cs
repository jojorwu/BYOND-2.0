namespace Shared;
    public class Chunk
    {
        public const int ChunkSize = 16;

        private readonly ITurf?[] _turfObjects;
        private readonly int[] _turfTypeIds;
        private int _version;
        public uint Version => (uint)_version;

        public Chunk()
        {
            _turfObjects = new ITurf?[ChunkSize * ChunkSize];
            _turfTypeIds = new int[ChunkSize * ChunkSize];
        }

        public ITurf? GetTurf(int x, int y)
        {
            if (x < 0 || x >= ChunkSize || y < 0 || y >= ChunkSize)
            {
                return null;
            }
            return _turfObjects[y * ChunkSize + x];
        }

        public int GetTurfTypeId(int x, int y)
        {
            if (x < 0 || x >= ChunkSize || y < 0 || y >= ChunkSize) return 0;
            return _turfTypeIds[y * ChunkSize + x];
        }

        public void SetTurf(int x, int y, ITurf turf)
        {
            if (x >= 0 && x < ChunkSize && y >= 0 && y < ChunkSize)
            {
                _turfObjects[y * ChunkSize + x] = turf;
                System.Threading.Interlocked.Increment(ref _version);
            }
        }

        public void SetTurfType(int x, int y, int typeId)
        {
            if (x >= 0 && x < ChunkSize && y >= 0 && y < ChunkSize)
            {
                _turfTypeIds[y * ChunkSize + x] = typeId;
                System.Threading.Interlocked.Increment(ref _version);
            }
        }

        public void ForEachTurf(System.Action<ITurf?, int, int> action)
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                for (int x = 0; x < ChunkSize; x++)
                {
                    action(_turfObjects[y * ChunkSize + x], x, y);
                }
            }
        }

        public IEnumerable<ITurf> GetTurfObjects()
        {
            for (int i = 0; i < _turfObjects.Length; i++)
            {
                if (_turfObjects[i] != null)
                {
                    yield return _turfObjects[i]!;
                }
            }
        }
    }
