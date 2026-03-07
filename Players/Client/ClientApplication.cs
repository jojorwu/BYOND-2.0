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
    using Shared.Services;

    public class ClientApplication : EngineApplication, IHostedService
    {
        private readonly Game _game;

        public ClientApplication(
            ILogger<ClientApplication> logger,
            IEnumerable<IEngineService> services,
            Game game) : base(logger, services)
        {
            _game = game;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Client Application...");
            await base.StartAsync(cancellationToken);
            _logger.LogInformation("Client Application started.");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Client Application...");
            await base.StopAsync(cancellationToken);
            _logger.LogInformation("Client Application stopped.");
        }
    }
}
