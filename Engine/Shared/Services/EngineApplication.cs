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
public abstract class EngineApplication : IHostedService
{
    protected readonly ILogger _logger;
    protected readonly List<IEngineService> _services;
    protected readonly List<IEngineModule> _modules;
    private ServiceDependencyGraph? _graph;
    protected virtual TimeSpan StartupTimeout => TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, ServiceStatus> _serviceHealth = new();
    public IReadOnlyDictionary<string, ServiceStatus> ServiceHealth => _serviceHealth;

    protected readonly IDiagnosticBus _diagnosticBus;

    protected EngineApplication(ILogger logger, IEnumerable<IEngineService> services, IEnumerable<IEngineModule> modules, IDiagnosticBus diagnosticBus)
    {
        _logger = logger;
        _services = services.ToList();
        _modules = modules.ToList();
        _diagnosticBus = diagnosticBus;

        foreach (var service in _services)
        {
            var name = service.Name ?? service.GetType().Name;
            _serviceHealth[name] = ServiceStatus.Stopped;
        }

        _logger.LogInformation("{AppName} initialized with {ServiceCount} services and {ModuleCount} modules.", GetType().Name, _services.Count, _modules.Count);
    }

    private ServiceDependencyGraph GetGraph() => _graph ??= new ServiceDependencyGraph(_services);

    /// <summary>
    /// Starts all registered services in order of their dependency graph.
    /// </summary>
    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Starting {AppName} Lifecycle with Dependency Graph...", GetType().Name);

        var graph = GetGraph();
        var globalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        globalCts.CancelAfter(StartupTimeout);

        try
        {
            await graph.ExecuteParallelAsync(async service =>
            {
                var serviceName = service.Name ?? service.GetType().Name;
                try
                {
                    _logger.LogDebug("    -> Loading {ServiceName}...", serviceName);
                    _serviceHealth[serviceName] = ServiceStatus.Starting;

                    var initSw = System.Diagnostics.Stopwatch.StartNew();
                    await service.InitializeAsync();
                    initSw.Stop();

                    var startSw = System.Diagnostics.Stopwatch.StartNew();
                    await service.StartAsync(globalCts.Token);
                    startSw.Stop();

                    service.SetDurations(initSw.ElapsedMilliseconds, startSw.ElapsedMilliseconds);
                    _serviceHealth[serviceName] = ServiceStatus.Running;

                    _logger.LogInformation("    [OK] {ServiceName} loaded (Init: {Init}ms, Start: {Start}ms)",
                        serviceName,
                        service.InitializationDurationMs,
                        service.StartupDurationMs);

                    _diagnosticBus.Publish("EngineApplication", $"Service {serviceName} started", DiagnosticSeverity.Info, m =>
                    {
                        m["Service"] = serviceName;
                        m["InitializationDurationMs"] = service.InitializationDurationMs;
                        m["StartupDurationMs"] = service.StartupDurationMs;
                    });
                }
                catch (OperationCanceledException) when (globalCts.IsCancellationRequested)
                {
                    _serviceHealth[serviceName] = ServiceStatus.Failed;
                    _logger.LogError("    [TIMEOUT] Service {ServiceName} failed to start within {Timeout}ms", serviceName, StartupTimeout.TotalMilliseconds);

                    _diagnosticBus.Publish("EngineApplication", $"Service {serviceName} timeout", DiagnosticSeverity.Error, m =>
                    {
                        m["Service"] = serviceName;
                        m["TimeoutMs"] = StartupTimeout.TotalMilliseconds;
                    });

                    if (service.IsCritical) throw new TimeoutException($"Critical service {serviceName} timed out during startup.");
                }
                catch (Exception ex)
                {
                    _serviceHealth[serviceName] = ServiceStatus.Failed;
                    _logger.LogError(ex, "    [FAIL] Failed to start service: {ServiceName}", serviceName);

                    _diagnosticBus.Publish("EngineApplication", $"Service {serviceName} failed to start", DiagnosticSeverity.Critical, m =>
                    {
                        m["Service"] = serviceName;
                        m["Error"] = ex.Message;
                    });

                    if (service.IsCritical) throw;
                }
            });

            // Post-Initialize lifecycle stage
            await Task.WhenAll(_services.OfType<IEngineLifecycle>().Select(s => s.PostInitializeAsync(globalCts.Token)));
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
            m["Application"] = GetType().Name;
            m["TotalStartupDurationMs"] = sw.ElapsedMilliseconds;
        });

        await OnStartAsync(cancellationToken);

        // OnStarted lifecycle stage
        await Task.WhenAll(_services.OfType<IEngineLifecycle>().Select(s => s.OnStartedAsync(cancellationToken)));
    }

    /// <summary>
    /// Stops all registered services in reverse order of their dependencies.
    /// </summary>
    public virtual async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping {AppName} Lifecycle with Dependency Graph...", GetType().Name);

        // Pre-Shutdown lifecycle stage
        await Task.WhenAll(_services.OfType<IEngineLifecycle>().Select(s => s.PreShutdownAsync(cancellationToken)));

        await OnStopAsync(cancellationToken);

        var graph = GetGraph();

        try
        {
            await graph.ShutdownParallelAsync(async service =>
            {
                var serviceName = service.Name ?? service.GetType().Name;
                try
                {
                    _logger.LogDebug("    <- Stopping {ServiceName}...", serviceName);
                    _serviceHealth[serviceName] = ServiceStatus.Stopping;
                    await service.StopAsync(cancellationToken);
                    _serviceHealth[serviceName] = ServiceStatus.Stopped;
                    _logger.LogInformation("    [OK] {ServiceName} stopped", serviceName);
                }
                catch (Exception ex)
                {
                    _serviceHealth[serviceName] = ServiceStatus.Failed;
                    _logger.LogError(ex, "    [FAIL] Error stopping service: {ServiceName}", serviceName);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lifecycle shutdown failed.");
        }

        // Post-Shutdown lifecycle stage
        await Task.WhenAll(_services.OfType<IEngineLifecycle>().Select(s => s.PostShutdownAsync(cancellationToken)));

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
    /// Executes PostTick on all registered modules.
    /// </summary>
    public void PostTick()
    {
        foreach (var module in _modules)
        {
            module.PostTick();
        }
    }
}
