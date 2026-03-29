using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Regions;
using Robust.Shared.Maths;
using Shared;
using Microsoft.Extensions.Options;
using Shared.Interfaces;

namespace Server
{
    public class RegionalGameLoopStrategy : IGameLoopStrategy, IShrinkable
    {
        private readonly IScriptHost _scriptHost;
        private readonly IRegionManager _regionManager;
        private readonly IRegionActivationStrategy _regionActivationStrategy;
        private readonly IUdpServer _udpServer;
        private readonly IGameState _gameState;
        private readonly IGameStateSnapshotter _gameStateSnapshotter;
        private readonly IJobSystem _jobSystem;
        private readonly ServerSettings _settings;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<(long X, long Y, int Z), (long AggregateVersion, string Snapshot)> _snapshotCache = new();

        private List<MergedRegion> _mergedRegionsCache = new();
        private HashSet<Region> _activeRegionsCache = new();

        private static readonly Shared.Services.SharedPool<HashSet<long>> _idSetPool = new(() => new HashSet<long>(1024));
        private static readonly Shared.Services.SharedPool<List<IScriptThread>> _threadListPool = new(() => new List<IScriptThread>(128));

        public RegionalGameLoopStrategy(IScriptHost scriptHost, IRegionManager regionManager, IRegionActivationStrategy regionActivationStrategy, IUdpServer udpServer, IGameState gameState, IGameStateSnapshotter gameStateSnapshotter, IJobSystem jobSystem, IOptions<ServerSettings> settings)
        {
            _scriptHost = scriptHost;
            _regionManager = regionManager;
            _regionActivationStrategy = regionActivationStrategy;
            _udpServer = udpServer;
            _gameState = gameState;
            _gameStateSnapshotter = gameStateSnapshotter;
            _jobSystem = jobSystem;
            _settings = settings.Value;
        }

        public async Task TickAsync(CancellationToken cancellationToken)
        {
            var allThreads = _scriptHost.GetThreads();
            var globals = _threadListPool.Rent();

            foreach (var thread in allThreads)
            {
                if (thread.AssociatedObject == null)
                {
                    globals.Add(thread);
                }
                else
                {
                    var obj = thread.AssociatedObject;
                    if (obj.ActiveThreads == null)
                    {
                        obj.ActiveThreads = _threadListPool.Rent();
                    }
                    obj.ActiveThreads.Add(thread);
                }
            }

            var remainingGlobals = await _scriptHost.ExecuteThreadsAsync(globals, System.Linq.Enumerable.Empty<IGameObject>(), processGlobals: true);

            globals.Clear();
            _threadListPool.Return(globals);

            var activeRegions = _regionActivationStrategy.GetActiveRegions();
            List<MergedRegion> mergedRegions;

            if (activeRegions.SetEquals(_activeRegionsCache))
            {
                mergedRegions = _mergedRegionsCache;
            }
            else
            {
                mergedRegions = MergeRegions(activeRegions);
                _activeRegionsCache = new HashSet<Region>(activeRegions);
                _mergedRegionsCache = mergedRegions;
            }

            // Batch regions by workload to reduce scheduling overhead
            var batchedRegions = new List<List<(MergedRegion Region, List<IGameObject> Objects)>>();
            var currentBatch = new List<(MergedRegion Region, List<IGameObject> Objects)>();
            int currentBatchObjects = 0;
            const int TargetObjectsPerBatch = 500;

            int regionSize = _settings.Performance.RegionalProcessing.RegionSize;
            foreach (var region in mergedRegions)
            {
                var objs = new List<IGameObject>();
                region.GetGameObjects(_gameState, objs, regionSize);
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

            // Use internal JobSystem instead of Parallel.ForEachAsync for better integration with engine threading and priorities
            await _jobSystem.ForEachAsync(batchedRegions, (Func<List<(MergedRegion Region, List<IGameObject> Objects)>, Task>)(async batch =>
            {
                foreach (var (mergedRegion, gameObjects) in batch)
                {
                    var objectIds = _idSetPool.Rent();
                    var threadsForRegion = _threadListPool.Rent();

                    try
                    {
                        var objSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(gameObjects);
                        for (int i = 0; i < objSpan.Length; i++)
                        {
                            var obj = objSpan[i];
                            objectIds.Add(obj.Id);
                            if (obj.ActiveThreads != null)
                            {
                                threadsForRegion.AddRange(obj.ActiveThreads);
                                obj.ActiveThreads.Clear();
                                _threadListPool.Return(obj.ActiveThreads);
                                obj.ActiveThreads = null;
                            }
                        }

                        // Execute threads for this region sequentially within this parallel job
                        var remainingRegionThreads = await _scriptHost.ExecuteThreadsAsync(threadsForRegion, gameObjects, objectIds: objectIds, forceSequential: true);
                        nextThreadsCollection.Add(remainingRegionThreads);
                    }
                    finally
                    {
                        objectIds.Clear();
                        _idSetPool.Return(objectIds);
                        threadsForRegion.Clear();
                        _threadListPool.Return(threadsForRegion);
                    }

                    // Update region versions after potential turf changes during script execution
                    foreach (var r in mergedRegion.Regions) r.UpdateVersion();

                    // Optimized aggregate version calculation
                    long aggregateVersion = 0;
                    var gameObjectSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(gameObjects);
                    for (int i = 0; i < gameObjectSpan.Length; i++) aggregateVersion += gameObjectSpan[i].Version;

                    var regSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(mergedRegion.Regions);
                    for (int i = 0; i < regSpan.Length; i++) aggregateVersion += regSpan[i].Version;

                    // Use merged region's first region as a cache key for simplicity
                    var firstRegion = mergedRegion.Regions[0];
                    var cacheKey = (firstRegion.Coords.X, firstRegion.Coords.Y, firstRegion.Z);

                    if (_settings.Network.EnableBinarySnapshots)
                    {
                        await _udpServer.SendRegionSnapshotAsync(mergedRegion, gameObjects);
                    }
                    else
                    {
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

                        _udpServer.BroadcastSnapshot(mergedRegion, snapshot);
                    }
                }
            }));

            var finalThreads = nextThreadsCollection.SelectMany(t => t);
            _scriptHost.UpdateThreads(finalThreads);
        }

        public void Shrink()
        {
            if (_snapshotCache.Count > 1000)
            {
                _snapshotCache.Clear();
            }
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

                                var neighborCoords = (current.Coords.X + dx, current.Coords.Y + dy);
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
