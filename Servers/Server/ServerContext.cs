using Shared;
using Core;
using Microsoft.Extensions.Options;
using Shared.Interfaces;

namespace Server
{
    public class ServerContext : IServerContext
    {
        public IGameState GameState { get; }
        public IPlayerManager PlayerManager { get; }
        public ServerSettings Settings { get; }
        public IRegionManager RegionManager { get; }
        public PerformanceMonitor PerformanceMonitor { get; }
        public IInterestManager InterestManager { get; }

        public ServerContext(IGameState gameState, IPlayerManager playerManager, IOptions<ServerSettings> settings, IRegionManager regionManager, PerformanceMonitor performanceMonitor, IInterestManager interestManager)
        {
            GameState = gameState;
            PlayerManager = playerManager;
            Settings = settings.Value;
            RegionManager = regionManager;
            PerformanceMonitor = performanceMonitor;
            InterestManager = interestManager;
        }
    }
}
