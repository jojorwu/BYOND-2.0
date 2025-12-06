using Shared;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Server
{
    public class Game : BackgroundService
    {
        private readonly GameState _gameState;
        private readonly ScriptHost _scriptHost;
        private readonly UdpServer _udpServer;
        private readonly ServerSettings _settings;
        private readonly ILogger<Game> _logger;
        private Task? _snapshotBroadcastTask;

        public Game(GameState gameState, ScriptHost scriptHost, UdpServer udpServer, ServerSettings settings, ILogger<Game> logger)
        {
            _gameState = gameState;
            _scriptHost = scriptHost;
            _udpServer = udpServer;
            _settings = settings;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _snapshotBroadcastTask = Task.Run(() => BroadcastSnapshots(stoppingToken), stoppingToken);

            var stopwatch = new Stopwatch();
            var tickInterval = TimeSpan.FromSeconds(1.0 / _settings.Performance.TickRate);

            _logger.LogInformation("Game loop started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                stopwatch.Restart();
                Tick();
                var elapsed = stopwatch.Elapsed;
                var sleepTime = tickInterval - elapsed;
                if (sleepTime > TimeSpan.Zero)
                {
                    await Task.Delay(sleepTime, stoppingToken);
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
    }
}
