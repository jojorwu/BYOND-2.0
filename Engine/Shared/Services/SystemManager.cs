using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared.Attributes;
using Shared.Interfaces;
using Shared.Models;
using Shared.Enums;

namespace Shared.Services;

public interface ISystemManager
{
    Task TickAsync();
}

[EngineService(typeof(ISystemManager))]
public class SystemManager : EngineService, ISystemManager, ITickable, IAsyncDisposable
{
    private record SystemExecutionInfo(ISystem System, IEntityQuery[] Queries)
    {
        public readonly Dictionary<int, Func<object, object, IEntityCommandBuffer, ValueTask>> ChunkTickers = new();
        public readonly Dictionary<int, Func<object, int, System.Collections.IEnumerable>> ChunkProviders = new();
    }

    private static readonly ExecutionPhase[] Phases = Enum.GetValues<ExecutionPhase>();
    private readonly ISystemRegistry _registry;
    private readonly ISystemExecutionPlanner _planner;
    private SystemExecutionInfo[][]?[] _phaseExecutionLayers = new SystemExecutionInfo[Phases.Length][][];
    private readonly Dictionary<ISystem, SystemExecutionInfo> _systemInfoCache = new();
    private readonly IProfilingService _profilingService;
    private readonly IJobSystem _jobSystem;
    private readonly IObjectPool<EntityCommandBuffer> _ecbPool;
    private readonly IArchetypeManager _archetypeManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IComponentQueryService _queryService;
    private readonly IDiagnosticBus _diagnosticBus;
    private readonly IEnumerable<ISystem> _initialSystems;
    private bool _isDirty = true;

    public SystemManager(
        ISystemRegistry registry,
        ISystemExecutionPlanner planner,
        IProfilingService profilingService,
        IJobSystem jobSystem,
        IObjectPool<EntityCommandBuffer> ecbPool,
        IArchetypeManager archetypeManager,
        IEnumerable<ISystem> systems,
        ILoggerFactory loggerFactory,
        IComponentQueryService queryService,
        IDiagnosticBus diagnosticBus)
    {
        _registry = registry;
        _planner = planner;
        _profilingService = profilingService;
        _jobSystem = jobSystem;
        _ecbPool = ecbPool;
        _archetypeManager = archetypeManager;
        _loggerFactory = loggerFactory;
        _queryService = queryService;
        _diagnosticBus = diagnosticBus;
        _initialSystems = systems;

        _registry.SystemsChanged += MarkDirty;
    }

    protected override Task OnInitializeAsync()
    {
        foreach (var system in _initialSystems)
        {
            var info = InitializeSystem(system);
            _systemInfoCache[system] = info;
        }
        _registry.RegisterRange(_initialSystems);
        return Task.CompletedTask;
    }

    ValueTask ITickable.TickAsync()
    {
        return new ValueTask(TickAsync());
    }

    private SystemExecutionInfo InitializeSystem(ISystem system)
    {
        var queries = new List<IEntityQuery>();
        var info = new SystemExecutionInfo(system, Array.Empty<IEntityQuery>());
        var type = system.GetType();

        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        foreach (var field in fields)
        {
            if (field.GetCustomAttribute<QueryAttribute>() != null && typeof(EntityQuery).IsAssignableFrom(field.FieldType))
            {
                var query = (IEntityQuery)CreateEntityQuery(field.FieldType);
                field.SetValue(system, query);
                queries.Add(query);
            }
        }

        // Also check base types for fields
        var baseType = type.BaseType;
        while (baseType != null && baseType != typeof(object))
        {
            var baseFields = baseType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var field in baseFields)
            {
                if (field.GetCustomAttribute<QueryAttribute>() != null && typeof(EntityQuery).IsAssignableFrom(field.FieldType))
                {
                    if (field.GetValue(system) == null)
                    {
                        var query = (IEntityQuery)CreateEntityQuery(field.FieldType);
                        field.SetValue(system, query);
                        queries.Add(query);
                    }
                }
            }
            baseType = baseType.BaseType;
        }

        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        foreach (var prop in properties)
        {
            if (prop.GetCustomAttribute<QueryAttribute>() != null && typeof(EntityQuery).IsAssignableFrom(prop.PropertyType) && prop.CanWrite)
            {
                var query = (IEntityQuery)CreateEntityQuery(prop.PropertyType);
                prop.SetValue(system, query);
                queries.Add(query);
            }
        }

        system.Initialize();

        // Cache chunk tickers for high-performance execution
        foreach (var query in queries)
        {
            var queryType = query.GetType();
            if (queryType.IsGenericType && queryType.GetGenericTypeDefinition() == typeof(EntityQuery<>))
            {
                var componentType = queryType.GetGenericArguments()[0];
                var chunkType = typeof(ArchetypeChunk<>).MakeGenericType(componentType);
                var tickMethod = system.GetType().GetMethod("TickAsync", [chunkType, typeof(IEntityCommandBuffer)]);

                if (tickMethod != null && tickMethod.DeclaringType != typeof(ISystem))
                {
                    // Create a delegate to avoid reflection in the hot path
                    // We use a helper to avoid generic issues with Action/Func
                    info.ChunkTickers[query.GetHashCode()] = CreateChunkTicker(tickMethod, chunkType);

                    var getChunksMethod = queryType.GetMethod("GetChunks");
                    if (getChunksMethod != null)
                    {
                        info.ChunkProviders[query.GetHashCode()] = CreateChunkProvider(getChunksMethod, queryType);
                    }
                }
            }
        }

        return info with { Queries = queries.ToArray() };
    }

    private Func<object, object, IEntityCommandBuffer, ValueTask> CreateChunkTicker(MethodInfo method, Type chunkType)
    {
        var systemParam = Expression.Parameter(typeof(object), "system");
        var chunkParam = Expression.Parameter(typeof(object), "chunk");
        var ecbParam = Expression.Parameter(typeof(IEntityCommandBuffer), "ecb");

        var castSystem = Expression.Convert(systemParam, method.DeclaringType!);
        var castChunk = Expression.Convert(chunkParam, chunkType);

        var call = Expression.Call(castSystem, method, castChunk, ecbParam);
        return Expression.Lambda<Func<object, object, IEntityCommandBuffer, ValueTask>>(call, systemParam, chunkParam, ecbParam).Compile();
    }

    private Func<object, int, System.Collections.IEnumerable> CreateChunkProvider(MethodInfo method, Type queryType)
    {
        var queryParam = Expression.Parameter(typeof(object), "query");
        var sizeParam = Expression.Parameter(typeof(int), "size");
        var castQuery = Expression.Convert(queryParam, queryType);
        var call = Expression.Call(castQuery, method, sizeParam);
        var castResult = Expression.Convert(call, typeof(System.Collections.IEnumerable));
        return Expression.Lambda<Func<object, int, System.Collections.IEnumerable>>(castResult, queryParam, sizeParam).Compile();
    }

    private EntityQuery CreateEntityQuery(Type queryType)
    {
        if (queryType.IsGenericType)
        {
            return (EntityQuery)Activator.CreateInstance(queryType, _queryService)!;
        }
        return (EntityQuery)Activator.CreateInstance(queryType, _queryService, Array.Empty<Type>())!;
    }

    public void MarkDirty() => _isDirty = true;

    private void RebuildExecutionLayers()
    {
        var systems = _registry.GetSystems();
        foreach (var system in systems)
        {
            if (!_systemInfoCache.TryGetValue(system, out var info))
            {
                info = InitializeSystem(system);
                _systemInfoCache[system] = info;
            }
        }

        var layers = _planner.PlanExecution(systems, Phases);
        for (int i = 0; i < Phases.Length; i++)
        {
            if (layers[i] == null)
            {
                _phaseExecutionLayers[i] = null;
                continue;
            }

            _phaseExecutionLayers[i] = layers[i].Select(layer =>
                layer.Select(s =>
                {
                    if (!_systemInfoCache.TryGetValue(s, out var info))
                    {
                        info = InitializeSystem(s);
                        _systemInfoCache[s] = info;
                    }
                    return info;
                }).ToArray()
            ).ToArray();
        }
        _isDirty = false;
    }

    public async Task TickAsync()
    {
        if (_isDirty)
        {
            RebuildExecutionLayers();
        }

        var totalSw = System.Diagnostics.Stopwatch.StartNew();
        using (_profilingService.Measure("SystemManager.Tick"))
        {
            using (_profilingService.Measure("SystemManager.BeginUpdate"))
            {
                _archetypeManager.BeginUpdate();
            }

            // Execute Phases
            for (int i = 0; i < Phases.Length; i++)
            {
                var phase = Phases[i];
                var layers = _phaseExecutionLayers[i];
                if (layers == null) continue;

                var phaseSw = System.Diagnostics.Stopwatch.StartNew();
                using (_profilingService.Measure($"SystemManager.Phase.{phase}"))
                {
                    for (int j = 0; j < layers.Length; j++)
                    {
                        var layer = layers[j];
                        if (layer.Length == 1)
                        {
                            var info = layer[0];
                            var ecb = _ecbPool.Rent();
                            try
                            {
                                await ExecuteSystemAsync(info, ecb);
                                await _jobSystem.CompleteAllAsync();
                                using (_profilingService.Measure("SystemManager.ECBPlayback"))
                                {
                                    ecb.Playback();
                                }
                            }
                            finally
                            {
                                _ecbPool.Return(ecb);
                            }
                        }
                        else
                        {
                            // Optimized Parallel execution using engine JobSystem without redundant Task allocations
                            var ecbArray = System.Buffers.ArrayPool<EntityCommandBuffer>.Shared.Rent(layer.Length);
                            try
                            {
                                // Batch rent ECBs before starting the parallel loop to minimize pool contention inside the loop
                                for (int k = 0; k < layer.Length; k++) ecbArray[k] = _ecbPool.Rent();

                                await _jobSystem.ForEachAsync(layer, (info, index) =>
                                {
                                    var ecb = ecbArray[index];
                                    ExecuteSystemAsync(info, ecb).AsTask().Wait(); // Sync over async for parallel wrapper
                                });

                                await _jobSystem.CompleteAllAsync();

                                using (_profilingService.Measure("SystemManager.ECBPlayback"))
                                {
                                    for (int k = 0; k < layer.Length; k++)
                                    {
                                        var ecb = ecbArray[k];
                                        if (ecb != null)
                                        {
                                            ecb.Playback();
                                            _ecbPool.Return(ecb);
                                            ecbArray[k] = null!;
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                System.Buffers.ArrayPool<EntityCommandBuffer>.Shared.Return(ecbArray, clearArray: true);
                            }
                        }
                    }
                }

                _diagnosticBus.Publish("SystemManager", "Phase execution completed", (Phase: phase, Duration: phaseSw.Elapsed.TotalMilliseconds), (m, state) =>
                {
                    m.Add("Phase", state.Phase.ToString());
                    m.Add("DurationMs", state.Duration);
                });
            }

            using (_profilingService.Measure("SystemManager.CommitUpdate"))
            {
                _archetypeManager.CommitUpdate();
            }

            // Cleanup Phase: Reset all worker arenas
            using (_profilingService.Measure("SystemManager.Cleanup"))
            {
                await _jobSystem.ResetAllArenasAsync();
            }
        }
    }

    private async ValueTask ExecuteSystemAsync(SystemExecutionInfo info, IEntityCommandBuffer ecb)
    {
        if (!info.System.Enabled) return;
        var system = info.System;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using (_profilingService.Measure($"System.{system.Name}"))
        {
            system.PreTick();

            bool batchHandled = false;
            var queries = info.Queries;

            if (queries.Length > 0)
            {
                // New high-performance chunked processing path
                bool handledByChunks = false;
                for (int i = 0; i < queries.Length; i++)
                {
                    var query = queries[i];
                    int qHash = query.GetHashCode();
                    if (info.ChunkTickers.TryGetValue(qHash, out var ticker) &&
                        info.ChunkProviders.TryGetValue(qHash, out var provider))
                    {
                        var chunks = provider(query, 1024);
                        foreach (var chunk in chunks)
                        {
                            var vt = ticker(system, chunk, ecb);
                            if (!vt.IsCompleted) await vt;
                            handledByChunks = true;
                            batchHandled = true;
                        }
                    }
                }

                if (!handledByChunks)
                {
                    if (system.ParallelArchetypes)
                    {
                        var allMatching = new List<Archetype>();
                        for (int i = 0; i < queries.Length; i++)
                        {
                            allMatching.AddRange(queries[i].GetMatchingArchetypes());
                        }

                        if (allMatching.Count > 0)
                        {
                            await _jobSystem.ForEachAsync(allMatching, arch => system.TickAsync(arch, ecb));
                            batchHandled = true;
                        }
                    }
                    else
                    {
                        // Sequential processing of archetypes
                        for (int i = 0; i < queries.Length; i++)
                        {
                            var query = queries[i];
                            foreach (var archetype in query.GetMatchingArchetypes())
                            {
                                await system.TickAsync(archetype, ecb);
                                batchHandled = true;
                            }
                        }
                    }
                }
            }

            if (!batchHandled)
            {
                await system.TickAsync(ecb);
            }

            system.PostTick();

            _diagnosticBus.Publish("SystemManager", "System execution completed", (System: system.Name, Duration: sw.Elapsed.TotalMilliseconds), (m, state) =>
            {
                m.Add("System", state.System);
                m.Add("DurationMs", state.Duration);
            });

            var jobs = system.CreateJobs();
            if (jobs != null)
            {
                foreach (var job in jobs)
                {
                    _jobSystem.Schedule(job);
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _registry.SystemsChanged -= MarkDirty;

        foreach (var system in _registry.GetSystems())
        {
            await system.ShutdownAsync();
        }
        GC.SuppressFinalize(this);
    }
}
