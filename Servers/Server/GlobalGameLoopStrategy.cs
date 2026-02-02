using System.Threading;
using System.Threading.Tasks;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;

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

        public Task TickAsync(CancellationToken cancellationToken)
        {
            _scriptHost.Tick();
            var snapshot = _gameStateSnapshotter.GetSnapshot(_gameState);
            _ = Task.Run(() => _udpServer.BroadcastSnapshot(snapshot), cancellationToken);
            return Task.CompletedTask;
        }
    }
}
