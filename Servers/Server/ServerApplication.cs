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
        private readonly CVarReplicator _replicator;
        private readonly Shared.Config.IConsoleCommandManager _commandManager;

        public ServerApplication(
            ILogger<ServerApplication> logger,
            IEnumerable<IEngineService> services,
            CVarReplicator replicator,
            Shared.Config.IConsoleCommandManager commandManager)
        {
            _logger = logger;
            _services = services.ToList();
            _replicator = replicator;
            _commandManager = commandManager;
            _logger.LogInformation("ServerApplication initialized with {Count} services.", _services.Count);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("========================================");
            _logger.LogInformation("BYOND 2.0 Server - Starting Up");
            _logger.LogInformation("========================================");

            // Group by priority and start independent services in parallel
            var priorityGroups = _services
                .GroupBy(s => s.Priority)
                .OrderByDescending(g => g.Key)
                .Select(g => g.ToList())
                .ToList();

            foreach (var group in priorityGroups)
            {
                _logger.LogInformation("Starting Service Group (Priority: {Priority})...", group[0].Priority);

                var tasks = group.Select(async service =>
                {
                    try
                    {
                        var serviceName = service.GetType().Name;
                        _logger.LogInformation("  -> Loading {ServiceName}...", serviceName);
                        var serviceSw = System.Diagnostics.Stopwatch.StartNew();
                        await service.InitializeAsync();
                        await service.StartAsync(cancellationToken);
                        serviceSw.Stop();
                        _logger.LogInformation("  [OK] {ServiceName} loaded in {Elapsed}ms", serviceName, serviceSw.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "  [FAIL] Failed to start service: {ServiceName}", service.GetType().Name);
                        if (service.IsCritical)
                        {
                            throw; // Rethrow to abort startup
                        }
                    }
                });

                await Task.WhenAll(tasks);
            }

            sw.Stop();
            _logger.LogInformation("========================================");
            _logger.LogInformation("Server started successfully in {Elapsed}ms", sw.ElapsedMilliseconds);
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
