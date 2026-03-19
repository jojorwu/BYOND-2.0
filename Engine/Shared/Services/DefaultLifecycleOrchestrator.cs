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
    private readonly ServiceDependencyGraph _graph;
    private readonly Dictionary<string, ServiceStatus> _serviceHealth = new();

    public IReadOnlyDictionary<string, ServiceStatus> ServiceHealth => _serviceHealth;

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);

    public DefaultLifecycleOrchestrator(
        ILogger<DefaultLifecycleOrchestrator> logger,
        IDiagnosticBus diagnosticBus,
        IEnumerable<IEngineService> services)
    {
        _logger = logger;
        _diagnosticBus = diagnosticBus;
        _services = services;
        _graph = new ServiceDependencyGraph(_services);

        foreach (var service in _services)
        {
            var name = service.Name ?? service.GetType().Name;
            _serviceHealth[name] = ServiceStatus.Stopped;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var globalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        globalCts.CancelAfter(StartupTimeout);

        await _graph.ExecuteParallelAsync(async service =>
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

                _diagnosticBus.Publish("LifecycleOrchestrator", $"Service {serviceName} started", DiagnosticSeverity.Info, m =>
                {
                    m.Add("Service", serviceName);
                    m.Add("InitializationDurationMs", service.InitializationDurationMs);
                    m.Add("StartupDurationMs", service.StartupDurationMs);
                });
            }
            catch (OperationCanceledException) when (globalCts.IsCancellationRequested)
            {
                _serviceHealth[serviceName] = ServiceStatus.Failed;
                _logger.LogError("    [TIMEOUT] Service {ServiceName} failed to start within {Timeout}ms", serviceName, StartupTimeout.TotalMilliseconds);

                _diagnosticBus.Publish("LifecycleOrchestrator", $"Service {serviceName} timeout", DiagnosticSeverity.Error, m =>
                {
                    m.Add("Service", serviceName);
                    m.Add("TimeoutMs", StartupTimeout.TotalMilliseconds);
                });

                if (service.IsCritical) throw new TimeoutException($"Critical service {serviceName} timed out during startup.");
            }
            catch (Exception ex)
            {
                _serviceHealth[serviceName] = ServiceStatus.Failed;
                _logger.LogError(ex, "    [FAIL] Failed to start service: {ServiceName}", serviceName);

                _diagnosticBus.Publish("LifecycleOrchestrator", $"Service {serviceName} failed to start", DiagnosticSeverity.Critical, m =>
                {
                    m.Add("Service", serviceName);
                    m.Add("Error", ex.Message);
                });

                if (service.IsCritical) throw;
            }
        });

        // Post-Initialize lifecycle stage
        await Task.WhenAll(_services.OfType<IEngineLifecycle>().Select(s => s.PostInitializeAsync(globalCts.Token)));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Pre-Shutdown lifecycle stage
        await Task.WhenAll(_services.OfType<IEngineLifecycle>().Select(s => s.PreShutdownAsync(cancellationToken)));

        try
        {
            await _graph.ShutdownParallelAsync(async service =>
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
    }
}
