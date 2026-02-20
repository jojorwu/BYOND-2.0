using System.Threading;
using System.Threading.Tasks;
using Shared;

namespace Server
{
    public class GlobalGameLoopStrategy : IGameLoopStrategy
    {
        private readonly IScriptHost _scriptHost;
        private readonly IGameState _gameState;
        private readonly IGameStateSnapshotter _gameStateSnapshotter;
        private readonly IUdpServer _udpServer;

        public GlobalGameLoopStrategy(IScriptHost scriptHost, IGameState gameState, IGameStateSnapshotter gameStateSnapshotter, IUdpServer udpServer)
        {
            _scriptHost = scriptHost;
            _gameState = gameState;
            _gameStateSnapshotter = gameStateSnapshotter;
            _udpServer = udpServer;
        }

        public async Task TickAsync(CancellationToken cancellationToken)
        {
            await _scriptHost.TickAsync();
            var snapshot = _gameStateSnapshotter.GetSnapshot(_gameState);
            _ = Task.Run(() => _udpServer.BroadcastSnapshot(snapshot), cancellationToken);
        }
    }
}
