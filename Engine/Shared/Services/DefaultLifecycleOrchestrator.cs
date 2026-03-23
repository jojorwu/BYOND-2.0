using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared.Interfaces;

namespace Shared.Services;

public class DefaultLifecycleOrchestrator : ILifecycleOrchestrator
{
    private readonly ILogger<DefaultLifecycleOrchestrator> _logger;
    private readonly IDiagnosticBus _diagnosticBus;
    private readonly IEnumerable<IEngineService> _services;
    private readonly IEnumerable<IEngineLifecycle> _lifecycles;
    private readonly ServiceDependencyGraph _graph;
    private readonly Dictionary<string, ServiceStatus> _serviceHealth = new();

    public IReadOnlyDictionary<string, ServiceStatus> ServiceHealth => _serviceHealth;

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);

    public DefaultLifecycleOrchestrator(
        ILogger<DefaultLifecycleOrchestrator> logger,
        IDiagnosticBus diagnosticBus,
        IEnumerable<IEngineService> services,
        IEnumerable<IEngineLifecycle> lifecycles)
    {
        _logger = logger;
        _diagnosticBus = diagnosticBus;
        _services = services;
        _lifecycles = lifecycles;
        _graph = new ServiceDependencyGraph(_services);

        foreach (var service in _services)
        {
            var name = service.Name ?? service.GetType().Name;
            _serviceHealth[name] = ServiceStatus.Stopped;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(() => MonitorHealthAsync(cancellationToken), cancellationToken);

        var globalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        globalCts.CancelAfter(StartupTimeout);

        // Phase 1: InitializeAsync (Parallel with Dependencies)
        _logger.LogInformation("Starting Service Initialization Phase...");
        await _graph.ExecuteParallelAsync(async service =>
        {
            var serviceName = service.Name ?? service.GetType().Name;
            try
            {
                _logger.LogDebug("    -> Initializing {ServiceName}...", serviceName);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                await service.InitializeAsync();
                sw.Stop();
                service.SetDurations(sw.ElapsedMilliseconds, 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "    [FAIL] Initialization failed for service: {ServiceName}", serviceName);
                if (service.IsCritical) throw;
            }
        });

        // Phase 2: PostInitializeAsync (Parallel for all EngineLifecycles)
        _logger.LogInformation("Executing Post-Initialization Hooks...");
        await Task.WhenAll(_lifecycles.Select(s => s.PostInitializeAsync(globalCts.Token)));

        // Phase 3: StartAsync (Parallel with Dependencies)
        _logger.LogInformation("Starting Service Execution Phase...");
        await _graph.ExecuteParallelAsync(async service =>
        {
            var serviceName = service.Name ?? service.GetType().Name;
            try
            {
                _logger.LogDebug("    -> Starting {ServiceName}...", serviceName);
                _serviceHealth[serviceName] = ServiceStatus.Starting;
                service.SetStatus(ServiceStatus.Starting);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                await service.StartAsync(globalCts.Token);
                sw.Stop();

                service.SetDurations(service.InitializationDurationMs, sw.ElapsedMilliseconds);
                _serviceHealth[serviceName] = ServiceStatus.Running;
                service.SetStatus(ServiceStatus.Running);

                _logger.LogInformation("    [OK] {ServiceName} started ({Start}ms)", serviceName, sw.ElapsedMilliseconds);

                _diagnosticBus.Publish("LifecycleOrchestrator", $"Service {serviceName} started", DiagnosticSeverity.Info, m =>
                {
                    m.Add("Service", serviceName);
                    m.Add("InitMs", service.InitializationDurationMs);
                    m.Add("StartMs", service.StartupDurationMs);
                });
            }
            catch (OperationCanceledException) when (globalCts.IsCancellationRequested)
            {
                _serviceHealth[serviceName] = ServiceStatus.Failed;
                service.SetStatus(ServiceStatus.Failed);
                _logger.LogError("    [TIMEOUT] Service {ServiceName} failed to start within {Timeout}ms", serviceName, StartupTimeout.TotalMilliseconds);
                if (service.IsCritical) throw new TimeoutException($"Critical service {serviceName} timed out during startup.");
            }
            catch (Exception ex)
            {
                _serviceHealth[serviceName] = ServiceStatus.Failed;
                service.SetStatus(ServiceStatus.Failed);
                _logger.LogError(ex, "    [FAIL] Start failed for service: {ServiceName}", serviceName);
                if (service.IsCritical) throw;
            }
        });

        // Phase 4: OnStartedAsync (Parallel for all EngineLifecycles)
        _logger.LogInformation("Executing OnStarted Hooks...");
        await Task.WhenAll(_lifecycles.Select(s => s.OnStartedAsync(globalCts.Token)));
    }

    private async Task MonitorHealthAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!cancellationToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                foreach (var service in _services)
                {
                    var name = service.Name ?? service.GetType().Name;
                    var result = await service.CheckHealthAsync(cancellationToken);

                    if (result.Status == HealthStatus.Unhealthy)
                    {
                        _logger.LogWarning("Service {ServiceName} is unhealthy: {Description}", name, result.Description);
                        _diagnosticBus.Publish("HealthMonitor", $"Service {name} unhealthy", DiagnosticSeverity.Warning, m =>
                        {
                            m.Add("Service", name);
                            m.Add("Description", result.Description ?? "No description");
                        });
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring service health");
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Engine Shutdown Sequence...");

        // Phase 1: PreShutdownAsync (Parallel for all EngineLifecycles)
        _logger.LogInformation("Executing Pre-Shutdown Hooks...");
        await Task.WhenAll(_lifecycles.Select(s => s.PreShutdownAsync(cancellationToken)));

        // Phase 2: StopAsync (Parallel with Reverse Dependencies)
        _logger.LogInformation("Stopping Services...");
        try
        {
            await _graph.ShutdownParallelAsync(async service =>
            {
                var serviceName = service.Name ?? service.GetType().Name;
                try
                {
                    _logger.LogDebug("    <- Stopping {ServiceName}...", serviceName);
                    _serviceHealth[serviceName] = ServiceStatus.Stopping;
                    service.SetStatus(ServiceStatus.Stopping);
                    await service.StopAsync(cancellationToken);
                    _serviceHealth[serviceName] = ServiceStatus.Stopped;
                    service.SetStatus(ServiceStatus.Stopped);
                    _logger.LogInformation("    [OK] {ServiceName} stopped", serviceName);
                }
                catch (Exception ex)
                {
                    _serviceHealth[serviceName] = ServiceStatus.Failed;
                    service.SetStatus(ServiceStatus.Failed);
                    _logger.LogError(ex, "    [FAIL] Error stopping service: {ServiceName}", serviceName);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service shutdown phase failed.");
        }

        // Phase 3: PostShutdownAsync (Parallel for all EngineLifecycles)
        _logger.LogInformation("Executing Post-Shutdown Hooks...");
        await Task.WhenAll(_lifecycles.Select(s => s.PostShutdownAsync(cancellationToken)));

        _logger.LogInformation("Engine Shutdown Complete.");
    }
}
