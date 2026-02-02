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

            // Sort by priority (higher priority starts first)
            var sortedServices = _services
                .OrderByDescending(s => s.Priority)
                .ToList();

            foreach (var service in sortedServices)
            {
                var serviceName = service.GetType().Name;
                _logger.LogDebug("Starting service: {ServiceName} (Priority: {Priority})", serviceName, service.Priority);

                await service.InitializeAsync();
                await service.StartAsync(cancellationToken);
            }

            _logger.LogInformation("Server Application started successfully.");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Server Application...");

            // Stop in reverse order of startup (lower priority stops first)
            var sortedServices = _services
                .OrderBy(s => s.Priority)
                .ToList();

            foreach (var service in sortedServices)
            {
                var serviceName = service.GetType().Name;
                _logger.LogDebug("Stopping service: {ServiceName}", serviceName);

                try
                {
                    await service.StopAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping service: {ServiceName}", serviceName);
                }
            }

            _logger.LogInformation("Server Application stopped.");
        }
    }
}
