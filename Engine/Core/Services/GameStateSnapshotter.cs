using System.Text.Json;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;

namespace Core
{
    public class GameStateSnapshotter : IGameStateSnapshotter
    {
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
    }
}
