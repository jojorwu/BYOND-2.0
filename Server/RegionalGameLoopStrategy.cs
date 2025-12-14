using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Robust.Shared.Maths;
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
            var mergedRegions = MergeRegions(activeRegions);

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

        internal List<MergedRegion> MergeRegions(HashSet<Region> activeRegions)
        {
            if (!_settings.Performance.RegionalProcessing.EnableRegionMerging || activeRegions.Count < _settings.Performance.RegionalProcessing.MinRegionsToMerge)
            {
                return activeRegions.Select(r => new MergedRegion(new List<Region> { r })).ToList();
            }

            var mergedRegions = new List<MergedRegion>();
            var visited = new HashSet<Region>();

            var regionsByZ = activeRegions.GroupBy(r => r.Z)
                .ToDictionary(g => g.Key, g => g.ToHashSet());

            foreach (var z in regionsByZ.Keys)
            {
                foreach (var region in regionsByZ[z])
                {
                    if (visited.Contains(region))
                        continue;

                    var group = new List<Region>();
                    var queue = new Queue<Region>();

                    queue.Enqueue(region);
                    visited.Add(region);

                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        group.Add(current);

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (Math.Abs(dx) == Math.Abs(dy)) continue;

                                var neighborCoords = new Vector2i(current.Coords.X + dx, current.Coords.Y + dy);
                                if (_regionManager.TryGetRegion(z, neighborCoords, out var neighbor) && regionsByZ[z].Contains(neighbor) && !visited.Contains(neighbor))
                                {
                                    visited.Add(neighbor);
                                    queue.Enqueue(neighbor);
                                }
                            }
                        }
                    }
                    mergedRegions.Add(new MergedRegion(group));
                }
            }
            return mergedRegions;
        }
    }
}
