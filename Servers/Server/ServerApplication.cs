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
        private readonly List<IHostedService> _services;

        public ServerApplication(
            ILogger<ServerApplication> logger,
            PerformanceMonitor performanceMonitor,
            IScriptHost scriptHost,
            IUdpServer udpServer,
            HttpServer httpServer,
            GameLoop gameLoop)
        {
            _logger = logger;

            // Define the explicit start order of services.
            _services = new List<IHostedService>
            {
                performanceMonitor,
                (IHostedService)scriptHost,
                (IHostedService)udpServer,
                httpServer,
                gameLoop
            };
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Server Application...");

            // Sort by priority (higher priority starts first)
            var sortedServices = _services
                .OrderByDescending(s => (s as IEngineService)?.Priority ?? 0)
                .ToList();

            foreach (var service in sortedServices)
            {
                var serviceName = service.GetType().Name;
                _logger.LogDebug("Starting service: {ServiceName}", serviceName);

                if (service is IAsyncInitializable initializable)
                {
                    await initializable.InitializeAsync();
                }

                await service.StartAsync(cancellationToken);
            }

            _logger.LogInformation("Server Application started successfully.");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Server Application...");

            // Stop in reverse order of startup (lower priority stops first)
            var sortedServices = _services
                .OrderBy(s => (s as IEngineService)?.Priority ?? 0)
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
