using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared.Interfaces;

namespace Shared.Services;

public abstract class EngineApplication
{
    protected readonly ILogger _logger;
    protected readonly List<IEngineService> _services;

    protected EngineApplication(ILogger logger, IEnumerable<IEngineService> services)
    {
        _logger = logger;
        _services = services.ToList();
    }

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
