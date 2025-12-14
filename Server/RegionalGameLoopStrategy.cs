using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shared;

namespace Server
{
    public class RegionalGameLoopStrategy : IGameLoopStrategy
    {
        private readonly IScriptHost _scriptHost;
        private readonly IRegionManager _regionManager;
        private readonly IUdpServer _udpServer;
        private readonly IGameState _gameState;
        private readonly ServerSettings _settings;

        public RegionalGameLoopStrategy(IScriptHost scriptHost, IRegionManager regionManager, IUdpServer udpServer, IGameState gameState, ServerSettings settings)
        {
            _scriptHost = scriptHost;
            _regionManager = regionManager;
            _udpServer = udpServer;
            _gameState = gameState;
            _settings = settings;
        }

        public async Task TickAsync(CancellationToken cancellationToken)
        {
            var globals = _scriptHost.GetThreads().Where(t => t.AssociatedObject == null).ToList();
            var remainingGlobals = _scriptHost.ExecuteThreads(globals, System.Linq.Enumerable.Empty<IGameObject>(), processGlobals: true);

            var activeRegions = _regionManager.GetActiveRegions();
            var mergedRegions = _regionManager.MergeRegions(activeRegions);

            var regionData = new System.Collections.Concurrent.ConcurrentBag<(MergedRegion, string, IEnumerable<IGameObject>)>();
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = _settings.Performance.RegionalProcessing.MaxThreads > 0
                    ? _settings.Performance.RegionalProcessing.MaxThreads
                    : -1
            };

            await Parallel.ForEachAsync(mergedRegions, options, (mergedRegion, token) =>
            {
                var gameObjects = mergedRegion.GetGameObjects().ToList();
                regionData.Add((mergedRegion, _gameState.GetSnapshot(mergedRegion), gameObjects));
                return ValueTask.CompletedTask;
            });

            var tasks = new List<Task<IEnumerable<IScriptThread>>>();
            var allThreads = _scriptHost.GetThreads();
            foreach(var (mergedRegion, snapshot, gameObjects) in regionData)
            {
                tasks.Add(Task.Run(() => _scriptHost.ExecuteThreads(allThreads, gameObjects), cancellationToken));
                _ = Task.Run(() => _udpServer.BroadcastSnapshot(mergedRegion, snapshot), cancellationToken);
            }

            var remainingThreads = new List<IScriptThread>(remainingGlobals);
            foreach (var task in tasks)
            {
                remainingThreads.AddRange(await task);
            }
            _scriptHost.UpdateThreads(remainingThreads.Distinct());
        }
    }
}
