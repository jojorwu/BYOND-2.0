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

    public class ServerApplication : Shared.Services.EngineApplication, IServer
    {
        private readonly CVarReplicator _replicator;
        private readonly Shared.Config.IConsoleCommandManager _commandManager;

        public ServerApplication(
            ILogger<ServerApplication> logger,
            IEnumerable<IEngineService> services,
            IEnumerable<IEngineModule> modules,
            IEnumerable<ITickable> tickables,
            IEnumerable<IShrinkable> shrinkables,
            IDiagnosticBus diagnosticBus,
            CVarReplicator replicator,
            Shared.Config.IConsoleCommandManager commandManager,
            ILifecycleOrchestrator orchestrator)
            : base(logger, services, modules, tickables, shrinkables, diagnosticBus)
        {
            _replicator = replicator;
            _commandManager = commandManager;
            SetOrchestrator(orchestrator);
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("========================================");
            _logger.LogInformation("Server started successfully.");
            _logger.LogInformation("Ready for connections. Type commands below:");
            _logger.LogInformation("========================================");

            _ = Task.Run(() => RunConsoleLoop());
            return Task.CompletedTask;
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

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Shutting down Server Application...");
            return Task.CompletedTask;
        }
    }
}
