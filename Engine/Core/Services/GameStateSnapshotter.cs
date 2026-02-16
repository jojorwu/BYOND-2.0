using System.Text.Json;
using Shared;
using Shared.Services;

namespace Core
{
    public class GameStateSnapshotter : EngineService, IGameStateSnapshotter
    {
        private readonly BinarySnapshotService _binarySnapshotService;

        public GameStateSnapshotter(BinarySnapshotService binarySnapshotService)
        {
            _binarySnapshotService = binarySnapshotService;
        }

        public string GetSnapshot(IGameState gameState)
        {
            using (gameState.ReadLock())
            {
                var snapshot = new
                {
                    gameState.Map,
                    gameState.GameObjects
                };
                return JsonSerializer.Serialize(snapshot);
            }
        }

        public string GetSnapshot(IGameState gameState, MergedRegion region)
        {
            using (gameState.ReadLock())
            {
                var snapshot = new
                {
                    GameObjects = region.GetGameObjects()
                };
                return JsonSerializer.Serialize(snapshot);
            }
        }

        public string GetSnapshot(IGameState gameState, Region region)
        {
            using (gameState.ReadLock())
            {
                var snapshot = new
                {
                    GameObjects = region.GetGameObjects()
                };
                return JsonSerializer.Serialize(snapshot);
            }
        }

        public byte[] GetBinarySnapshot(IGameState gameState, MergedRegion mergedRegion)
        {
            using (gameState.ReadLock())
            {
                return _binarySnapshotService.Serialize(mergedRegion.GetGameObjects());
            }
        }
    }
}
