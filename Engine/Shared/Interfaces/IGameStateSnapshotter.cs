using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
namespace Shared.Interfaces
{
    public interface IGameStateSnapshotter
    {
        string GetSnapshot(IGameState gameState);
        string GetSnapshot(IGameState gameState, Region region);
        string GetSnapshot(IGameState gameState, MergedRegion mergedRegion);
    }
}
