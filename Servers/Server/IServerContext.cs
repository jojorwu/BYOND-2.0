using Shared;
using Core;
using Shared.Interfaces;

namespace Server
{
    public interface IServerContext
    {
        IGameState GameState { get; }
        IPlayerManager PlayerManager { get; }
        ServerSettings Settings { get; }
        IRegionManager RegionManager { get; }
        PerformanceMonitor PerformanceMonitor { get; }
        IInterestManager InterestManager { get; }
    }
}
