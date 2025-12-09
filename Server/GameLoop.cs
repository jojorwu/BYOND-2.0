using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Shared;

namespace Server
{
    public class GameLoop : IHostedService, IDisposable
    {
        private readonly IScriptHost _scriptHost;
        private readonly IUdpServer _udpServer;
        private readonly IGameState _gameState;
        private readonly ServerSettings _settings;
        private Task? _gameLoopTask;
        private CancellationTokenSource? _cancellationTokenSource;

        public GameLoop(IScriptHost scriptHost, IUdpServer udpServer, IGameState gameState, ServerSettings settings)
        {
            _scriptHost = scriptHost;
            _udpServer = udpServer;
            _gameState = gameState;
            _settings = settings;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _gameLoopTask = Task.Run(() => Loop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            return Task.CompletedTask;
        }

        private async Task Loop(CancellationToken token)
        {
            var tickRate = _settings.Performance.TickRate;
            var interval = TimeSpan.FromSeconds(1.0 / tickRate);
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            while (!token.IsCancellationRequested)
            {
                var elapsed = stopwatch.Elapsed;

                if (elapsed >= interval)
                {
                    _scriptHost.Tick();
                    var snapshot = _gameState.GetSnapshot();
                    // Don't block the main game loop with network operations
                    _ = Task.Run(() => _udpServer.BroadcastSnapshot(snapshot), token);
                    stopwatch.Restart();
                }

                // Yield the thread to prevent busy-waiting
                await Task.Delay(1, token);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource?.Cancel();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
        }
    }
}
