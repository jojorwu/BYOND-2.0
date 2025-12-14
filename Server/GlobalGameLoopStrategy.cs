using System.Threading;
using System.Threading.Tasks;
using Shared;

namespace Server
{
    public class GlobalGameLoopStrategy : IGameLoopStrategy
    {
        private readonly IScriptHost _scriptHost;
        private readonly IGameState _gameState;
        private readonly IUdpServer _udpServer;

        public GlobalGameLoopStrategy(IScriptHost scriptHost, IGameState gameState, IUdpServer udpServer)
        {
            _scriptHost = scriptHost;
            _gameState = gameState;
            _udpServer = udpServer;
        }

        public Task TickAsync(CancellationToken cancellationToken)
        {
            _scriptHost.Tick();
            var snapshot = _gameState.GetSnapshot();
            _ = Task.Run(() => _udpServer.BroadcastSnapshot(snapshot), cancellationToken);
            return Task.CompletedTask;
        }
    }
}
