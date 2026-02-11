using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Interfaces;

namespace Server
{
    public interface IServer
    {
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }

    public class ServerApplication : IServer, IHostedService
    {
        private readonly ILogger<ServerApplication> _logger;
        private readonly List<IEngineService> _services;

        public ServerApplication(
            ILogger<ServerApplication> logger,
            IEnumerable<IEngineService> services)
        {
            _logger = logger;
            _services = services.ToList();
            _logger.LogInformation("ServerApplication initialized with {Count} services.", _services.Count);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Server Application...");

            // Group by priority and start independent services in parallel
            var priorityGroups = _services
                .GroupBy(s => s.Priority)
                .OrderByDescending(g => g.Key)
                .Select(g => g.ToList())
                .ToList();

            foreach (var group in priorityGroups)
            {
                if (group.Count == 1)
                {
                    var service = group[0];
                    _logger.LogDebug("Starting service: {ServiceName} (Priority: {Priority})", service.GetType().Name, service.Priority);
                    await service.InitializeAsync();
                    await service.StartAsync(cancellationToken);
                }
                else
                {
                    _logger.LogDebug("Starting group of {Count} services in parallel (Priority: {Priority})", group.Count, group[0].Priority);
                    await Task.WhenAll(group.Select(async service =>
                    {
                        await service.InitializeAsync();
                        await service.StartAsync(cancellationToken);
                    }));
                }
            }

            _logger.LogInformation("Server Application started successfully.");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Server Application...");

            // Stop in reverse order of startup (lower priority group stops first)
            var priorityGroups = _services
                .GroupBy(s => s.Priority)
                .OrderBy(g => g.Key)
                .Select(g => g.ToList())
                .ToList();

            foreach (var group in priorityGroups)
            {
                if (group.Count == 1)
                {
                    var service = group[0];
                    _logger.LogDebug("Stopping service: {ServiceName}", service.GetType().Name);
                    try { await service.StopAsync(cancellationToken); }
                    catch (Exception ex) { _logger.LogError(ex, "Error stopping service: {ServiceName}", service.GetType().Name); }
                }
                else
                {
                    _logger.LogDebug("Stopping group of {Count} services in parallel", group.Count);
                    await Task.WhenAll(group.Select(async service =>
                    {
                        try { await service.StopAsync(cancellationToken); }
                        catch (Exception ex) { _logger.LogError(ex, "Error stopping service: {ServiceName}", service.GetType().Name); }
                    }));
                }
            }

            _logger.LogInformation("Server Application stopped.");
        }
    }
}
