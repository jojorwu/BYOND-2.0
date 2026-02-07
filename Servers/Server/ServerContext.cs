using Shared;
using Core;
using Microsoft.Extensions.Options;

namespace Server
{
    public class ServerContext : IServerContext
    {
        public IGameState GameState { get; }
        public IPlayerManager PlayerManager { get; }
        public ServerSettings Settings { get; }
        public IRegionManager RegionManager { get; }
        public PerformanceMonitor PerformanceMonitor { get; }

        public ServerContext(IGameState gameState, IPlayerManager playerManager, IOptions<ServerSettings> settings, IRegionManager regionManager, PerformanceMonitor performanceMonitor)
        {
            GameState = gameState;
            PlayerManager = playerManager;
            Settings = settings.Value;
            RegionManager = regionManager;
            PerformanceMonitor = performanceMonitor;
        }
    }
}
