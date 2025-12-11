using Shared;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Maths;
using System.Threading.Tasks;

namespace Server
{
    public class RegionManager
    {
        private readonly IGameState _gameState;
        private readonly IScriptHost _scriptHost;
        private readonly Dictionary<Vector2i, Region> _regions = new();
        private readonly ServerSettings _settings;

        public RegionManager(IGameState gameState, IScriptHost scriptHost, ServerSettings settings)
        {
            _gameState = gameState;
            _scriptHost = scriptHost;
            _settings = settings;
            InitializeRegions();
        }

        private void InitializeRegions()
        {
            if (_gameState.Map == null) return;

            foreach (var z in _gameState.Map.GetZLevels())
            {
                foreach (var (chunkCoords, chunk) in _gameState.Map.GetChunks(z))
                {
                    var regionCoords = chunkCoords; // For now, one chunk per region
                    if (!_regions.ContainsKey(regionCoords))
                    {
                        var chunksInRegion = new Dictionary<Vector2i, Chunk> { { chunkCoords, chunk } };
                        _regions[regionCoords] = new Region(regionCoords, chunksInRegion, _scriptHost);
                    }
                }
            }
        }

        public IEnumerable<Region> GetRegions()
        {
            return _regions.Values;
        }

        public void Tick()
        {
            // Note: Parallel.ForEach is NOT used here to avoid the race conditions
            // from the previous implementation. A more sophisticated threading model
            // would be required for safe parallel execution.
            foreach (var region in _regions.Values)
            {
                region.Tick();
            }
        }
    }
}
