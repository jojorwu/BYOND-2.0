using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Core;

namespace Server
{
    public interface IServerContext
    {
        IGameState GameState { get; }
        IPlayerManager PlayerManager { get; }
        ServerSettings Settings { get; }
        IRegionManager RegionManager { get; }
        PerformanceMonitor PerformanceMonitor { get; }
    }
}
