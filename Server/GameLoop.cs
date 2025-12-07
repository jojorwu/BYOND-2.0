using System;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;

namespace Server
{
    public class GameLoop : IHostedService, IDisposable
    {
        private readonly IScriptHost _scriptHost;
        private readonly IUdpServer _udpServer;
        private readonly IGameState _gameState;
        private readonly ServerSettings _settings;
        private readonly ILogger<GameLoop> _logger;
        private Task? _gameLoopTask;
        private Task? _snapshotBroadcastTask;
        private CancellationTokenSource? _cancellationTokenSource;

        public GameLoop(IScriptHost scriptHost, IUdpServer udpServer, IGameState gameState, ServerSettings settings, ILogger<GameLoop> logger)
        {
            _scriptHost = scriptHost;
            _udpServer = udpServer;
            _gameState = gameState;
            _settings = settings;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _gameLoopTask = Task.Run(() => Loop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            _snapshotBroadcastTask = Task.Run(() => BroadcastSnapshots(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            return Task.CompletedTask;
        }

        private async Task Loop(CancellationToken token)
        {
            var stopwatch = new Stopwatch();
            var tickInterval = TimeSpan.FromSeconds(1.0 / _settings.Performance.TickRate);
            _logger.LogInformation("Game loop started.");

            while (!token.IsCancellationRequested)
            {
                stopwatch.Restart();
                Tick();
                var elapsed = stopwatch.Elapsed;
                var sleepTime = tickInterval - elapsed;
                if (sleepTime > TimeSpan.Zero)
                {
                    await Task.Delay(sleepTime, token);
                }
            }
            _logger.LogInformation("Game loop stopped.");
        }

        private void Tick()
        {
            _scriptHost.Tick();
        }

        private async Task BroadcastSnapshots(CancellationToken cancellationToken)
        {
            var snapshotInterval = TimeSpan.FromMilliseconds(_settings.Performance.SnapshotBroadcastInterval);
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var snapshot = _gameState.GetSnapshot();
                    _udpServer.BroadcastSnapshot(snapshot);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during snapshot generation/broadcast.");
                }
                await Task.Delay(snapshotInterval, cancellationToken);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                var tasks = new[] { _gameLoopTask, _snapshotBroadcastTask }.Where(t => t != null).ToArray();
                if (tasks.Length > 0)
                {
                    try
                    {
                        await Task.WhenAll(tasks!);
                    }
                    catch (TaskCanceledException)
                    {
                        // Expected, tasks are canceled.
                    }
                }
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
        }
    }
}
