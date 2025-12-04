using System;
using System.Diagnostics;
using System.Threading;
using Core;
using Microsoft.Extensions.DependencyInjection;

namespace Server
{
    public class Game : IDisposable
    {
        private readonly GameState _gameState;
        private readonly ScriptHost _scriptHost;
        private readonly UdpServer _udpServer;
        private readonly ServerSettings _settings;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private Task? _snapshotBroadcastTask;
        private bool _isRunning = true;

        public Game(GameState gameState, ScriptHost scriptHost, UdpServer udpServer, ServerSettings settings)
        {
            _gameState = gameState;
            _scriptHost = scriptHost;
            _udpServer = udpServer;
            _settings = settings;
        }

        public async Task Start()
        {
            _scriptHost.Start();
            _udpServer.Start();
            _snapshotBroadcastTask = Task.Run(() => BroadcastSnapshots(_cancellationTokenSource.Token));

            var stopwatch = new Stopwatch();
            var tickInterval = TimeSpan.FromSeconds(1.0 / _settings.Performance.TickRate);

            while (_isRunning && !_cancellationTokenSource.IsCancellationRequested)
            {
                stopwatch.Restart();
                Tick();
                var elapsed = stopwatch.Elapsed;
                var sleepTime = tickInterval - elapsed;
                if (sleepTime > TimeSpan.Zero)
                {
                    await Task.Delay(sleepTime, _cancellationTokenSource.Token);
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _cancellationTokenSource.Cancel();
            _snapshotBroadcastTask?.Wait(); // Wait for the task to finish
        }

        private void Tick()
        {
            _scriptHost.Tick();
        }

        private async Task BroadcastSnapshots(CancellationToken cancellationToken)
        {
            var snapshotInterval = TimeSpan.FromSeconds(1.0 / _settings.Network.SnapshotRate);
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var snapshot = _gameState.GetSnapshot();
                    _udpServer.BroadcastSnapshot(snapshot);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during snapshot generation/broadcast: {ex}");
                }
                await Task.Delay(snapshotInterval, cancellationToken);
            }
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource.Dispose();
        }
    }
}
