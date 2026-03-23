using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

public class SystemManager : EngineService, ISystemManager, ITickable, IAsyncDisposable
{
    private record SystemExecutionInfo(ISystem System, IEntityQuery[] Queries);

    private static readonly ExecutionPhase[] Phases = Enum.GetValues<ExecutionPhase>();
    private readonly ISystemRegistry _registry;
    private readonly ISystemExecutionPlanner _planner;
    private SystemExecutionInfo[][]?[] _phaseExecutionLayers = new SystemExecutionInfo[Phases.Length][][];
    private readonly Dictionary<ISystem, SystemExecutionInfo> _systemInfoCache = new();
    private readonly IProfilingService _profilingService;
    private readonly IJobSystem _jobSystem;
    private readonly IObjectPool<EntityCommandBuffer> _ecbPool;
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

    Task ITickable.TickAsync()
    {
        return TickAsync();
    }

    private SystemExecutionInfo InitializeSystem(ISystem system)
    {
        var queries = new List<IEntityQuery>();
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
        return new SystemExecutionInfo(system, queries.ToArray());
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
        var layers = _planner.PlanExecution(_registry.GetSystems(), Phases);
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
                            var ecbArray = System.Buffers.ArrayPool<EntityCommandBuffer>.Shared.Rent(layer.Length);
                            try
                            {
                                await Task.WhenAll(layer.Select(async (info, index) =>
                                {
                                    var ecb = _ecbPool.Rent();
                                    ecbArray[index] = ecb;
                                    await ExecuteSystemAsync(info, ecb);
                                }));

                                await _jobSystem.CompleteAllAsync();

                                using (_profilingService.Measure("SystemManager.ECBPlayback"))
                                {
                                    for (int k = 0; k < layer.Length; k++)
                                    {
                                        var ecb = ecbArray[k];
                                        ecb?.Playback();
                                        if (ecb != null) _ecbPool.Return(ecb);
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

                _diagnosticBus.Publish("SystemManager", $"Phase {phase} execution completed", DiagnosticSeverity.Info, m =>
                {
                    m.Add("Phase", phase.ToString());
                    m.Add("DurationMs", phaseSw.Elapsed.TotalMilliseconds);
                });
            }

            // Cleanup Phase: Reset all worker arenas
            using (_profilingService.Measure("SystemManager.Cleanup"))
            {
                await _jobSystem.ResetAllArenasAsync();
            }
        }
    }

    private async Task ExecuteSystemAsync(SystemExecutionInfo info, IEntityCommandBuffer ecb)
    {
        var system = info.System;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using (_profilingService.Measure($"System.{system.Name}"))
        {
            system.PreTick();

            bool batchHandled = false;
            var queries = info.Queries;
            if (queries.Length > 0)
            {
                if (system.ParallelArchetypes)
                {
                    var matchingArchetypes = new List<Archetype>();
                    for (int i = 0; i < queries.Length; i++)
                    {
                        matchingArchetypes.AddRange(queries[i].GetMatchingArchetypes());
                    }

                    if (matchingArchetypes.Count > 0)
                    {
                        await _jobSystem.ForEachAsync(matchingArchetypes, async arch =>
                        {
                            await system.TickAsync(arch, ecb);
                        });
                        batchHandled = true;
                    }
                }
                else
                {
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

            if (!batchHandled)
            {
                await system.TickAsync(ecb);
            }

            system.PostTick();

            _diagnosticBus.Publish("SystemManager", $"System {system.Name} execution completed", DiagnosticSeverity.Info, m =>
            {
                m.Add("System", system.Name);
                m.Add("DurationMs", sw.Elapsed.TotalMilliseconds);
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
