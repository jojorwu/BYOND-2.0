using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Regions;
using Robust.Shared.Maths;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;

namespace Server
{
    public class RegionalGameLoopStrategy : IGameLoopStrategy
    {
        private readonly IScriptHost _scriptHost;
        private readonly IRegionManager _regionManager;
        private readonly IRegionActivationStrategy _regionActivationStrategy;
        private readonly IUdpServer _udpServer;
        private readonly IGameState _gameState;
        private readonly IGameStateSnapshotter _gameStateSnapshotter;
        private readonly ServerSettings _settings;

        public RegionalGameLoopStrategy(IScriptHost scriptHost, IRegionManager regionManager, IRegionActivationStrategy regionActivationStrategy, IUdpServer udpServer, IGameState gameState, IGameStateSnapshotter gameStateSnapshotter, ServerSettings settings)
        {
            _scriptHost = scriptHost;
            _regionManager = regionManager;
            _regionActivationStrategy = regionActivationStrategy;
            _udpServer = udpServer;
            _gameState = gameState;
            _gameStateSnapshotter = gameStateSnapshotter;
            _settings = settings;
        }

        public async Task TickAsync(CancellationToken cancellationToken)
        {
            var allThreads = _scriptHost.GetThreads();
            var globals = allThreads.Where(t => t.AssociatedObject == null).ToList();
            var remainingGlobals = _scriptHost.ExecuteThreads(globals, System.Linq.Enumerable.Empty<IGameObject>(), processGlobals: true);

            var activeRegions = _regionActivationStrategy.GetActiveRegions();
            var mergedRegions = MergeRegions(activeRegions);

            var objectThreads = allThreads.Where(t => t.AssociatedObject != null)
                .GroupBy(t => t.AssociatedObject!.Id)
                .ToDictionary(g => g.Key, g => g.ToList());

            var regionData = new System.Collections.Concurrent.ConcurrentBag<(MergedRegion, string, List<IScriptThread>, HashSet<int>, List<IGameObject>)>();
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = _settings.Performance.RegionalProcessing.MaxThreads > 0
                    ? _settings.Performance.RegionalProcessing.MaxThreads
                    : -1
            };

            await Parallel.ForEachAsync(mergedRegions, options, (mergedRegion, token) =>
            {
                var gameObjects = mergedRegion.GetGameObjects().ToList();
                var objectIds = new HashSet<int>(gameObjects.Select(o => o.Id));
                var threadsForRegion = gameObjects.SelectMany(obj => objectThreads.GetValueOrDefault(obj.Id, new List<IScriptThread>())).ToList();

                regionData.Add((mergedRegion, _gameStateSnapshotter.GetSnapshot(_gameState, mergedRegion), threadsForRegion, objectIds, gameObjects));
                return ValueTask.CompletedTask;
            });

            var tasks = new List<Task<IEnumerable<IScriptThread>>>();
            foreach(var (mergedRegion, snapshot, threadsForRegion, objectIds, gameObjects) in regionData)
            {
                tasks.Add(Task.Run(() => _scriptHost.ExecuteThreads(threadsForRegion, gameObjects, objectIds: objectIds), cancellationToken));
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
