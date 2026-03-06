using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Interfaces;
using Shared.Services;

namespace Server
{
    public interface IServer
    {
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }

    public class ServerApplication : EngineApplication, IServer, IHostedService
    {
        private readonly CVarReplicator _replicator;
        private readonly Shared.Config.IConsoleCommandManager _commandManager;

        public ServerApplication(
            ILogger<ServerApplication> logger,
            IEnumerable<IEngineService> services,
            CVarReplicator replicator,
            Shared.Config.IConsoleCommandManager commandManager) : base(logger, services)
        {
            _replicator = replicator;
            _commandManager = commandManager;
            _logger.LogInformation("ServerApplication initialized with {Count} services.", _services.Count);
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("========================================");
            _logger.LogInformation("BYOND 2.0 Server - Starting Up");
            _logger.LogInformation("========================================");

            await base.StartAsync(cancellationToken);

            _logger.LogInformation("========================================");
            _logger.LogInformation("Server started successfully");
            _logger.LogInformation("Ready for connections. Type commands below:");
            _logger.LogInformation("========================================");

            _ = Task.Run(() => RunConsoleLoop());
        }

        private async Task RunConsoleLoop()
        {
            while (true)
            {
                var input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) continue;

                var result = await _commandManager.ExecuteCommand(input);
                if (!string.IsNullOrEmpty(result))
                {
                    _logger.LogInformation(result);
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Server Application...");
            await base.StopAsync(cancellationToken);
            _logger.LogInformation("Server Application stopped.");
        }
    }
}
