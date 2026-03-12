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
    protected virtual TimeSpan StartupTimeout => TimeSpan.FromSeconds(30);

    protected EngineApplication(ILogger logger, IEnumerable<IEngineService> services)
    {
        _logger = logger;
        _services = services.ToList();
        _logger.LogInformation("{AppName} initialized with {Count} services.", GetType().Name, _services.Count);
    }

    /// <summary>
    /// Starts all registered services in order of their dependency graph.
    /// </summary>
    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Starting {AppName} Lifecycle with Dependency Graph...", GetType().Name);

        var graph = new ServiceDependencyGraph(_services);
        var globalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        globalCts.CancelAfter(StartupTimeout);

        try
        {
            await graph.ExecuteParallelAsync(async service =>
            {
                var serviceName = service.GetType().Name;
                try
                {
                    _logger.LogDebug("    -> Loading {ServiceName}...", serviceName);
                    var serviceSw = System.Diagnostics.Stopwatch.StartNew();

                    await service.InitializeAsync();
                    await service.StartAsync(globalCts.Token);

                    serviceSw.Stop();
                    _logger.LogInformation("    [OK] {ServiceName} loaded in {Elapsed}ms", serviceName, serviceSw.ElapsedMilliseconds);
                }
                catch (OperationCanceledException) when (globalCts.IsCancellationRequested)
                {
                    _logger.LogError("    [TIMEOUT] Service {ServiceName} failed to start within {Timeout}ms", serviceName, StartupTimeout.TotalMilliseconds);
                    if (service.IsCritical) throw new TimeoutException($"Critical service {serviceName} timed out during startup.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "    [FAIL] Failed to start service: {ServiceName}", serviceName);
                    if (service.IsCritical) throw;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Lifecycle initialization failed.");
            throw;
        }

        sw.Stop();
        _logger.LogInformation("{AppName} lifecycle started successfully in {Elapsed}ms", GetType().Name, sw.ElapsedMilliseconds);

        await OnStartAsync(cancellationToken);
    }

    /// <summary>
    /// Stops all registered services in reverse order of their dependencies.
    /// </summary>
    public virtual async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping {AppName} Lifecycle with Dependency Graph...", GetType().Name);

        await OnStopAsync(cancellationToken);

        var graph = new ServiceDependencyGraph(_services);

        try
        {
            await graph.ShutdownParallelAsync(async service =>
            {
                var serviceName = service.GetType().Name;
                try
                {
                    _logger.LogDebug("    <- Stopping {ServiceName}...", serviceName);
                    await service.StopAsync(cancellationToken);
                    _logger.LogInformation("    [OK] {ServiceName} stopped", serviceName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "    [FAIL] Error stopping service: {ServiceName}", serviceName);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lifecycle shutdown failed.");
        }

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
}
