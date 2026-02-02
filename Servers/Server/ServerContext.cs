using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Core;

namespace Server
{
    public class ServerContext : IServerContext
    {
        public IGameState GameState { get; }
        public IPlayerManager PlayerManager { get; }
        public ServerSettings Settings { get; }
        public IRegionManager RegionManager { get; }
        public PerformanceMonitor PerformanceMonitor { get; }

        public ServerContext(IGameState gameState, IPlayerManager playerManager, ServerSettings settings, IRegionManager regionManager, PerformanceMonitor performanceMonitor)
        {
            GameState = gameState;
            PlayerManager = playerManager;
            Settings = settings;
            RegionManager = regionManager;
            PerformanceMonitor = performanceMonitor;
        }
    }
}
