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
        private readonly IRegionManager _regionManager;
        private readonly ServerSettings _settings;
        private Task? _gameLoopTask;
        private CancellationTokenSource? _cancellationTokenSource;

        public GameLoop(IScriptHost scriptHost, IUdpServer udpServer, IGameState gameState, IRegionManager regionManager, ServerSettings settings)
        {
            _scriptHost = scriptHost;
            _udpServer = udpServer;
            _gameState = gameState;
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
                    if (_settings.Performance.EnableRegionalProcessing)
                    {
                        var globals = _scriptHost.GetThreads().Where(t => t.AssociatedObject == null).ToList();
                        var remainingGlobals = _scriptHost.ExecuteThreads(globals, System.Linq.Enumerable.Empty<IGameObject>(), processGlobals: true);

                        var regionData = await _regionManager.Tick();
                        var tasks = new List<Task<IEnumerable<IScriptThread>>>();
                        var allThreads = _scriptHost.GetThreads();
                        foreach(var (mergedRegion, snapshot, gameObjects) in regionData)
                        {
                            tasks.Add(Task.Run(() => _scriptHost.ExecuteThreads(allThreads, gameObjects), token));
                            _ = Task.Run(() => _udpServer.BroadcastSnapshot(mergedRegion, snapshot), token);
                        }

                        var remainingThreads = new List<IScriptThread>(remainingGlobals);
                        foreach (var task in tasks)
                        {
                            remainingThreads.AddRange(await task);
                        }
                        _scriptHost.UpdateThreads(remainingThreads.Distinct());
                    }
                    else
                    {
                        _scriptHost.Tick();
                        var snapshot = _gameState.GetSnapshot();
                        _ = Task.Run(() => _udpServer.BroadcastSnapshot(snapshot), token);
                    }
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
