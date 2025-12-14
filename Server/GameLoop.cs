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
        private readonly IGameLoopStrategy _strategy;
        private readonly IRegionManager _regionManager;
        private readonly ServerSettings _settings;
        private Task? _gameLoopTask;
        private CancellationTokenSource? _cancellationTokenSource;

        public GameLoop(IGameLoopStrategy strategy, IRegionManager regionManager, ServerSettings settings)
        {
            _strategy = strategy;
            _regionManager = regionManager;
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
            _regionManager.Initialize();
            var tickRate = _settings.Performance.TickRate;
            var interval = TimeSpan.FromSeconds(1.0 / tickRate);
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            while (!token.IsCancellationRequested)
            {
                var elapsed = stopwatch.Elapsed;

                if (elapsed >= interval)
                {
                    await _strategy.TickAsync(token);
                    stopwatch.Restart();
                }
                await Task.Delay(1, token);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_gameLoopTask == null)
                return;

            _cancellationTokenSource?.Cancel();
            try
            {
                await _gameLoopTask;
            }
            catch (TaskCanceledException)
            {
                // This is expected
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
        }
    }
}
