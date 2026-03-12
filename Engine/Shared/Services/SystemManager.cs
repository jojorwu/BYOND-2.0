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

public class SystemManager : ISystemManager, IAsyncDisposable
{
    private static readonly ExecutionPhase[] Phases = Enum.GetValues<ExecutionPhase>();
    private readonly ISystemRegistry _registry;
    private readonly ISystemExecutionPlanner _planner;
    private List<List<ISystem>>?[] _phaseExecutionLayers = new List<List<ISystem>>[Phases.Length];
    private readonly IProfilingService _profilingService;
    private readonly IJobSystem _jobSystem;
    private readonly IObjectPool<EntityCommandBuffer> _ecbPool;
    private readonly IEnumerable<IShrinkable> _shrinkables;
    private readonly IEnumerable<IEngineModule> _modules;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IComponentQueryService _queryService;
    private bool _isDirty = true;

    public SystemManager(
        ISystemRegistry registry,
        ISystemExecutionPlanner planner,
        IProfilingService profilingService,
        IJobSystem jobSystem,
        IObjectPool<EntityCommandBuffer> ecbPool,
        IEnumerable<IShrinkable> shrinkables,
        IEnumerable<IEngineModule> modules,
        IEnumerable<ISystem> systems,
        ILoggerFactory loggerFactory,
        IComponentQueryService queryService)
    {
        _registry = registry;
        _planner = planner;
        _profilingService = profilingService;
        _jobSystem = jobSystem;
        _ecbPool = ecbPool;
        _shrinkables = shrinkables;
        _modules = modules;
        _loggerFactory = loggerFactory;
        _queryService = queryService;

        foreach (var system in systems)
        {
            InitializeSystemQueries(system);
            system.Initialize();
            _registry.Register(system);
        }
    }

    private void InitializeSystemQueries(ISystem system)
    {
        var type = system.GetType();
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        foreach (var field in fields)
        {
            if (field.GetCustomAttribute<QueryAttribute>() != null && typeof(EntityQuery).IsAssignableFrom(field.FieldType))
            {
                var query = CreateEntityQuery(field.FieldType);
                field.SetValue(system, query);
            }
        }

        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        foreach (var prop in properties)
        {
            if (prop.GetCustomAttribute<QueryAttribute>() != null && typeof(EntityQuery).IsAssignableFrom(prop.PropertyType) && prop.CanWrite)
            {
                var query = CreateEntityQuery(prop.PropertyType);
                prop.SetValue(system, query);
            }
        }
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
        _phaseExecutionLayers = _planner.PlanExecution(_registry.GetSystems(), Phases);
        _isDirty = false;
    }

    public async Task TickAsync()
    {
        if (_isDirty)
        {
            RebuildExecutionLayers();
        }

        using (_profilingService.Measure("SystemManager.Tick"))
        {
            // Module Pre-Tick
            foreach (var module in _modules)
            {
                module.PreTick();
            }

            // Execute Phases
            for (int i = 0; i < Phases.Length; i++)
            {
                var layers = _phaseExecutionLayers[i];
                if (layers == null) continue;

                using (_profilingService.Measure($"SystemManager.Phase.{Phases[i]}"))
                {
                    foreach (var layer in layers)
                    {
                        if (layer.Count == 1)
                        {
                            var system = layer[0];
                            var ecb = _ecbPool.Rent();
                            try
                            {
                                ExecuteSystem(system, ecb);
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
                            var ecbArray = System.Buffers.ArrayPool<EntityCommandBuffer>.Shared.Rent(layer.Count);
                            try
                            {
                                await _jobSystem.ForEachAsync(layer, (system, index) =>
                                {
                                    var ecb = _ecbPool.Rent();
                                    ecbArray[index] = ecb;
                                    ExecuteSystem(system, ecb);
                                });

                                await _jobSystem.CompleteAllAsync();

                                using (_profilingService.Measure("SystemManager.ECBPlayback"))
                                {
                                    for (int j = 0; j < layer.Count; j++)
                                    {
                                        var ecb = ecbArray[j];
                                        ecb.Playback();
                                        _ecbPool.Return(ecb);
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
            }

            // Module Post-Tick
            foreach (var module in _modules)
            {
                module.PostTick();
            }

            // Cleanup Phase: Reset all worker arenas and shrink all registered pools/caches
            using (_profilingService.Measure("SystemManager.Cleanup"))
            {
                await _jobSystem.ResetAllArenasAsync();

                foreach (var shrinkable in _shrinkables)
                {
                    shrinkable.Shrink();
                }
            }
        }
    }

    private void ExecuteSystem(ISystem system, IEntityCommandBuffer ecb)
    {
        using (_profilingService.Measure($"System.{system.Name}"))
        {
            system.PreTick();

            // Batch processing: if the system has queries, execute against matching archetypes
            var type = system.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            bool batchHandled = false;

            foreach (var field in fields)
            {
                if (field.GetCustomAttribute<QueryAttribute>() != null && typeof(EntityQuery).IsAssignableFrom(field.FieldType))
                {
                    if (field.GetValue(system) is IEntityQuery query)
                    {
                        foreach (var archetype in query.GetMatchingArchetypes())
                        {
                            system.Tick(archetype, ecb);
                            batchHandled = true;
                        }
                    }
                }
            }

            if (!batchHandled)
            {
                system.Tick(ecb);
            }

            system.PostTick();

            var jobs = system.CreateJobs();
            foreach (var job in jobs)
            {
                _jobSystem.Schedule(job);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var system in _registry.GetSystems())
        {
            await system.ShutdownAsync();
        }
        GC.SuppressFinalize(this);
    }
}
