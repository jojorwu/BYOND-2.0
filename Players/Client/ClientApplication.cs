using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Interfaces;

namespace Client
{
    public class ClientApplication : IHostedService
    {
        private readonly ILogger<ClientApplication> _logger;
        private readonly List<IEngineService> _services;
        private readonly List<IEngineModule> _modules;
        private readonly IServiceProvider _serviceProvider;
        private readonly Game _game;

        public ClientApplication(
            ILogger<ClientApplication> logger,
            IEnumerable<IEngineService> services,
            IEnumerable<IEngineModule> modules,
            IServiceProvider serviceProvider,
            Game game)
        {
            _logger = logger;
            _services = services.ToList();
            _modules = modules.ToList();
            _serviceProvider = serviceProvider;
            _game = game;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Client Application...");

            foreach (var module in _modules)
            {
                try
                {
                    await module.InitializeAsync(_serviceProvider);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize module: {ModuleName}", module.Name);
                    if (module.IsCritical) throw;
                }
            }

            foreach (var service in _services.OrderByDescending(s => s.Priority))
            {
                try
                {
                    await service.InitializeAsync();
                    await service.StartAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start service: {ServiceName}", service.GetType().Name);
                    if (service.IsCritical) throw;
                }
            }

            _logger.LogInformation("Client Application started.");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Client Application...");

            foreach (var service in _services.OrderBy(s => s.Priority))
            {
                await service.StopAsync(cancellationToken);
            }

            foreach (var module in _modules)
            {
                await module.ShutdownAsync();
            }

            _logger.LogInformation("Client Application stopped.");
        }
    }
}
