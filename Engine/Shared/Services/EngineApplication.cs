using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Interfaces;

namespace Shared.Services;

/// <summary>
/// Base class for engine-based applications (Server, Client, Editor)
/// that manages a collection of <see cref="IEngineService"/> components.
/// </summary>
public abstract class EngineApplication : IHostedService, IEngine
{
    protected readonly ILogger _logger;
    protected readonly List<IEngineService> _services;
    protected readonly List<IEngineModule> _modules;
    protected readonly List<ITickable> _tickables = new();
    protected readonly List<IShrinkable> _shrinkables = new();
    protected readonly List<IEngineLifecycle> _lifecycles;
    protected readonly ITickable[][] _tickableGroups;
    private ILifecycleOrchestrator? _orchestrator;
    private IJobSystem? _jobSystem;

    public IReadOnlyDictionary<string, ServiceStatus> ServiceHealth => _orchestrator?.ServiceHealth ?? new Dictionary<string, ServiceStatus>();

    protected readonly IDiagnosticBus _diagnosticBus;
    private long _tickCount = 0;

    protected EngineApplication(
        ILogger logger,
        IEnumerable<IEngineService> services,
        IEnumerable<IEngineModule> modules,
        IEnumerable<ITickable> tickables,
        IEnumerable<IShrinkable> shrinkables,
        IEnumerable<IEngineLifecycle> lifecycles,
        IDiagnosticBus diagnosticBus)
    {
        _logger = logger;
        _services = services.ToList();
        _modules = modules.ToList();
        _tickables = tickables.ToList();
        _shrinkables = shrinkables.ToList();
        _lifecycles = lifecycles.ToList();
        _diagnosticBus = diagnosticBus;

        // Pre-calculate tickable groups by priority to avoid allocations in the hot path.
        _tickableGroups = _tickables
            .GroupBy(t => t is IEngineService service ? service.Priority : 0)
            .OrderByDescending(g => g.Key)
            .Select(g => g.ToArray())
            .ToArray();

        _jobSystem = _services.OfType<IJobSystem>().FirstOrDefault();

        _logger.LogInformation("{AppName} initialized with {ServiceCount} services, {ModuleCount} modules, {TickableCount} tickables, and {ShrinkableCount} shrinkables.",
            GetType().Name, _services.Count, _modules.Count, _tickables.Count, _shrinkables.Count);
    }

    protected void SetOrchestrator(ILifecycleOrchestrator orchestrator) => _orchestrator = orchestrator;

    /// <summary>
    /// Starts all registered services in order of their dependency graph.
    /// </summary>
    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_orchestrator == null) throw new InvalidOperationException("Lifecycle orchestrator not set.");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Starting {AppName} Lifecycle...", GetType().Name);

        try
        {
            await _orchestrator.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Lifecycle initialization failed.");
            throw;
        }

        sw.Stop();
        _logger.LogInformation("{AppName} lifecycle started successfully in {Elapsed}ms", GetType().Name, sw.ElapsedMilliseconds);

        _diagnosticBus.Publish("EngineApplication", $"{GetType().Name} lifecycle started", DiagnosticSeverity.Info, m =>
        {
            m.Add("Application", GetType().Name);
            m.Add("TotalStartupDurationMs", sw.ElapsedMilliseconds);
        });

        await OnStartAsync(cancellationToken);

        // OnStarted lifecycle stage
        await Task.WhenAll(_lifecycles.Select(s => s.OnStartedAsync(cancellationToken)));
    }

    /// <summary>
    /// Stops all registered services in reverse order of their dependencies.
    /// </summary>
    public virtual async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_orchestrator == null) throw new InvalidOperationException("Lifecycle orchestrator not set.");

        _logger.LogInformation("Stopping {AppName} Lifecycle...", GetType().Name);

        await OnStopAsync(cancellationToken);

        await _orchestrator.StopAsync(cancellationToken);

        _logger.LogInformation("{AppName} lifecycle stopped.", GetType().Name);
    }

    /// <summary>
    /// Hook for derived classes to perform actions after all services have started.
    /// </summary>
    protected virtual Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Hook for derived classes to perform actions before services begin stopping.
    /// </summary>
    protected virtual Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Executes PreTick on all registered modules.
    /// </summary>
    public void PreTick()
    {
        foreach (var module in _modules)
        {
            module.PreTick();
        }
    }

    /// <summary>
    /// Executes a standard engine tick.
    /// </summary>
    public virtual async Task TickAsync()
    {
        PreTick();

        // Optimized Parallel Ticking using pre-calculated groups and JobSystem.
        for (int i = 0; i < _tickableGroups.Length; i++)
        {
            var group = _tickableGroups[i];
            if (group.Length == 1)
            {
                await group[0].TickAsync();
            }
            else if (_jobSystem != null)
            {
                // Utilize the engine's JobSystem for parallel ticking of services in the same priority group.
                await _jobSystem.ForEachAsync(group, t => t.TickAsync());
            }
            else
            {
                // Fallback if JobSystem is not yet available.
                await Task.WhenAll(group.Select(t => t.TickAsync()));
            }
        }

        PostTick();

        if (Interlocked.Increment(ref _tickCount) % 100 == 0)
        {
            await MaintainAsync();
        }
    }

    /// <summary>
    /// Executes PostTick on all registered modules.
    /// </summary>
    public void PostTick()
    {
        foreach (var module in _modules)
        {
            module.PostTick();
        }
    }

    /// <summary>
    /// Performs periodic maintenance on all registered services in parallel.
    /// </summary>
    public virtual async Task MaintainAsync()
    {
        if (_jobSystem != null)
        {
            await _jobSystem.ForEachAsync(_shrinkables, s => s.Shrink());
        }
        else
        {
            foreach (var shrinkable in _shrinkables)
            {
                shrinkable.Shrink();
            }
        }
    }
}
