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
    public class ClientApplication : Shared.Services.EngineApplication
    {
        private readonly Game _game;

        public ClientApplication(
            ILogger<ClientApplication> logger,
            IEnumerable<IEngineService> services,
            IEnumerable<IEngineModule> modules,
            Game game)
            : base(logger, services, modules)
        {
            _game = game;
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Client Application UI ready.");
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Shutting down Client Application UI...");
            return Task.CompletedTask;
        }
    }
}
