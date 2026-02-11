using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Services;

namespace Server
{
    public class GameLoop : EngineService, IHostedService, IDisposable
    {
        public override int Priority => -100; // Low priority, start last
        private readonly IGameLoopStrategy _strategy;
        private readonly ISystemManager _systemManager;
        private readonly IServerContext _context;
        private readonly ILogger<GameLoop> _logger;
        private Task? _gameLoopTask;
        private CancellationTokenSource? _cancellationTokenSource;

        public GameLoop(IGameLoopStrategy strategy, ISystemManager systemManager, IServerContext context, ILogger<GameLoop> logger)
        {
            _strategy = strategy;
            _systemManager = systemManager;
            _context = context;
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _gameLoopTask = Task.Run(() => Loop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            return Task.CompletedTask;
        }

        private async Task Loop(CancellationToken token)
        {
            _logger.LogInformation("Game loop started.");
            _context.RegionManager.Initialize();

            var tickRate = _context.Settings.Performance.TickRate;
            var targetFrameTime = TimeSpan.FromSeconds(1.0 / tickRate);
            var stopwatch = Stopwatch.StartNew();
            var accumulator = TimeSpan.Zero;

            while (!token.IsCancellationRequested)
            {
                var elapsed = stopwatch.Elapsed;
                stopwatch.Restart();
                accumulator += elapsed;

                while (accumulator >= targetFrameTime)
                {
                    await _strategy.TickAsync(token);
                    _systemManager.Tick();

                    // Commit object state for consistent reading by other systems (e.g. networking/rendering)
                    _context.GameState.ForEachGameObject(obj => obj.CommitState());

                    _context.PerformanceMonitor.RecordTick();
                    accumulator -= targetFrameTime;
                }

                // If we are significantly behind, don't try to catch up too much to avoid "spiral of death"
                if (accumulator > targetFrameTime * 5)
                {
                    _logger.LogWarning("Server is falling behind! Skipping ticks.");
                    accumulator = TimeSpan.Zero;
                }

                // Adaptive delay to avoid pegged CPU but maintain precision
                var sleepTime = targetFrameTime - accumulator;
                if (sleepTime.TotalMilliseconds > 1)
                {
                    await Task.Delay(1, token);
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_gameLoopTask == null)
                return;

            _cancellationTokenSource?.Cancel();
            try
            {
                await _gameLoopTask;
            }
            catch (TaskCanceledException) { }
            _logger.LogInformation("Game loop stopped.");
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
        }
    }
}
