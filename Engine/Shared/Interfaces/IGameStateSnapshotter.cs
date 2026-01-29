namespace Shared
{
    public interface IGameStateSnapshotter
    {
        string GetSnapshot(IGameState gameState);
        string GetSnapshot(IGameState gameState, Region region);
        string GetSnapshot(IGameState gameState, MergedRegion mergedRegion);
    }
}
