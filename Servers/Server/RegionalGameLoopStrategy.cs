using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Regions;
using Robust.Shared.Maths;
using Shared;
using Microsoft.Extensions.Options;

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
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, (long AggregateVersion, string Snapshot)> _snapshotCache = new();

        public RegionalGameLoopStrategy(IScriptHost scriptHost, IRegionManager regionManager, IRegionActivationStrategy regionActivationStrategy, IUdpServer udpServer, IGameState gameState, IGameStateSnapshotter gameStateSnapshotter, IOptions<ServerSettings> settings)
        {
            _scriptHost = scriptHost;
            _regionManager = regionManager;
            _regionActivationStrategy = regionActivationStrategy;
            _udpServer = udpServer;
            _gameState = gameState;
            _gameStateSnapshotter = gameStateSnapshotter;
            _settings = settings.Value;
        }

        public async Task TickAsync(CancellationToken cancellationToken)
        {
            var allThreads = _scriptHost.GetThreads();
            var globals = new List<IScriptThread>();
            var objectThreads = new Dictionary<int, List<IScriptThread>>();

            foreach (var thread in allThreads)
            {
                if (thread.AssociatedObject == null)
                {
                    globals.Add(thread);
                }
                else
                {
                    int objId = thread.AssociatedObject.Id;
                    if (!objectThreads.TryGetValue(objId, out var threads))
                    {
                        threads = new List<IScriptThread>();
                        objectThreads[objId] = threads;
                    }
                    threads.Add(thread);
                }
            }

            var remainingGlobals = _scriptHost.ExecuteThreads(globals, System.Linq.Enumerable.Empty<IGameObject>(), processGlobals: true);

            var activeRegions = _regionActivationStrategy.GetActiveRegions();
            var mergedRegions = MergeRegions(activeRegions);

            // Batch regions by workload to reduce scheduling overhead
            var batchedRegions = new List<List<(MergedRegion Region, List<IGameObject> Objects)>>();
            var currentBatch = new List<(MergedRegion Region, List<IGameObject> Objects)>();
            int currentBatchObjects = 0;
            const int TargetObjectsPerBatch = 500;

            foreach (var region in mergedRegions)
            {
                var objs = new List<IGameObject>();
                region.GetGameObjects(objs);
                if (currentBatchObjects + objs.Count > TargetObjectsPerBatch && currentBatch.Count > 0)
                {
                    batchedRegions.Add(currentBatch);
                    currentBatch = new List<(MergedRegion Region, List<IGameObject> Objects)>();
                    currentBatchObjects = 0;
                }
                currentBatch.Add((region, objs));
                currentBatchObjects += objs.Count;
            }
            if (currentBatch.Count > 0) batchedRegions.Add(currentBatch);

            var nextThreadsCollection = new System.Collections.Concurrent.ConcurrentBag<IEnumerable<IScriptThread>>();
            nextThreadsCollection.Add(remainingGlobals);

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = _settings.Performance.RegionalProcessing.MaxThreads > 0
                    ? _settings.Performance.RegionalProcessing.MaxThreads
                    : -1,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(batchedRegions, options, async (batch, token) =>
            {
                foreach (var (mergedRegion, gameObjects) in batch)
                {
                    var objectIds = new HashSet<int>(gameObjects.Count);
                    var threadsForRegion = new List<IScriptThread>();

                    foreach (var obj in gameObjects)
                    {
                        objectIds.Add(obj.Id);
                        if (objectThreads.TryGetValue(obj.Id, out var threads))
                        {
                            threadsForRegion.AddRange(threads);
                        }
                    }

                    // Execute threads for this region
                    var remainingRegionThreads = _scriptHost.ExecuteThreads(threadsForRegion, gameObjects, objectIds: objectIds);
                    nextThreadsCollection.Add(remainingRegionThreads);

                    // Calculate aggregate version for cache check
                    long aggregateVersion = 0;
                    foreach (var obj in gameObjects) aggregateVersion += obj.Version;

                    // Use merged region's first region as a cache key for simplicity (or hash of all region coords)
                    long cacheKey = ((long)mergedRegion.Regions[0].Coords.X << 32) | (uint)mergedRegion.Regions[0].Coords.Y;

                    string snapshot;
                    if (_snapshotCache.TryGetValue(cacheKey, out var cached) && cached.AggregateVersion == aggregateVersion)
                    {
                        snapshot = cached.Snapshot;
                    }
                    else
                    {
                        snapshot = _gameStateSnapshotter.GetSnapshot(_gameState, mergedRegion);
                        _snapshotCache[cacheKey] = (aggregateVersion, snapshot);
                    }

                    await Task.Run(() => _udpServer.BroadcastSnapshot(mergedRegion, snapshot), token);
                }
            });

            var finalThreads = nextThreadsCollection.SelectMany(t => t);
            _scriptHost.UpdateThreads(finalThreads);
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
