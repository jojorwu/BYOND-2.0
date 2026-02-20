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
        private readonly Game _game;

        public ClientApplication(
            ILogger<ClientApplication> logger,
            IEnumerable<IEngineService> services,
            Game game)
        {
            _logger = logger;
            _services = services.ToList();
            _game = game;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Client Application...");

            foreach (var service in _services.OrderByDescending(s => s.Priority))
            {
                await service.InitializeAsync();
                await service.StartAsync(cancellationToken);
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

            _logger.LogInformation("Client Application stopped.");
        }
    }
}
