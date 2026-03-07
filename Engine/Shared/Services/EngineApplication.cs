using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared.Interfaces;

namespace Shared.Services;

/// <summary>
/// Provides a base class for orchestrating the engine application's lifecycle.
/// It manages the prioritized initialization, startup, and shutdown of all registered <see cref="IEngineService"/> components.
/// </summary>
public abstract class EngineApplication
{
    /// <summary> Logger instance for reporting application status. </summary>
    protected readonly ILogger _logger;
    /// <summary> List of all registered services, ordered by their defined priority. </summary>
    protected readonly List<IEngineService> _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="EngineApplication"/> class.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <param name="services">The collection of services discovered via DI.</param>
    protected EngineApplication(ILogger logger, IEnumerable<IEngineService> services)
    {
        _logger = logger;
        _services = services.ToList();
    }

    /// <summary>
    /// Asynchronously starts the application by starting all services in groups based on their priority.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the startup sequence.</param>
    /// <exception cref="Exception">Thrown if a critical service fails to start.</exception>
    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Starting Engine Application with {Count} services...", _services.Count);

        var priorityGroups = _services
            .GroupBy(s => s.Priority)
            .OrderByDescending(g => g.Key)
            .Select(g => g.ToList())
            .ToList();

        foreach (var group in priorityGroups)
        {
            _logger.LogDebug("Starting Service Group (Priority: {Priority})...", group[0].Priority);

            var tasks = group.Select(async service =>
            {
                try
                {
                    var serviceName = service.GetType().Name;
                    await service.InitializeAsync();
                    await service.StartAsync(cancellationToken);
                    _logger.LogDebug("  [OK] {ServiceName} started", serviceName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "  [FAIL] Failed to start service: {ServiceName}", service.GetType().Name);
                    if (service.IsCritical) throw;
                }
            });

            await Task.WhenAll(tasks);
        }

        sw.Stop();
        _logger.LogInformation("Engine Application started in {Elapsed}ms", sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Asynchronously stops the application by gracefully shutting down all services in reverse priority order.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the shutdown process.</param>
    public virtual async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Engine Application...");

        var priorityGroups = _services
            .GroupBy(s => s.Priority)
            .OrderBy(g => g.Key)
            .Select(g => g.ToList())
            .ToList();

        foreach (var group in priorityGroups)
        {
            await Task.WhenAll(group.Select(async service =>
            {
                try
                {
                    await service.StopAsync(cancellationToken);
                    _logger.LogDebug("  [OK] {ServiceName} stopped", service.GetType().Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping service: {ServiceName}", service.GetType().Name);
                }
            }));
        }

        _logger.LogInformation("Engine Application stopped.");
    }
}
