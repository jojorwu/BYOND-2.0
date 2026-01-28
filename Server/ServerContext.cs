using Shared;
using Core;

namespace Server
{
    public class ServerContext : IServerContext
    {
        public IGameState GameState { get; }
        public IPlayerManager PlayerManager { get; }
        public ServerSettings Settings { get; }
        public IRegionManager RegionManager { get; }

        public ServerContext(IGameState gameState, IPlayerManager playerManager, ServerSettings settings, IRegionManager regionManager)
        {
            GameState = gameState;
            PlayerManager = playerManager;
            Settings = settings;
            RegionManager = regionManager;
        }
    }
}
