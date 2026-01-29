using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;

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
        private readonly IScriptHost _scriptHost;
        private readonly IUdpServer _udpServer;
        private readonly IHostedService _gameLoop;
        private readonly IHostedService _httpServer;
        private readonly IHostedService _performanceMonitor;

        public ServerApplication(
            ILogger<ServerApplication> logger,
            IScriptHost scriptHost,
            IUdpServer udpServer,
            GameLoop gameLoop,
            HttpServer httpServer,
            PerformanceMonitor performanceMonitor)
        {
            _logger = logger;
            _scriptHost = scriptHost;
            _udpServer = udpServer;
            _gameLoop = gameLoop;
            _httpServer = httpServer;
            _performanceMonitor = performanceMonitor;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Server Application...");

            // Start order is important
            await _performanceMonitor.StartAsync(cancellationToken);
            await ((IHostedService)_scriptHost).StartAsync(cancellationToken);
            await ((IHostedService)_udpServer).StartAsync(cancellationToken);
            await _httpServer.StartAsync(cancellationToken);
            await _gameLoop.StartAsync(cancellationToken);

            _logger.LogInformation("Server Application started successfully.");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Server Application...");

            // Stop in reverse order
            await _gameLoop.StopAsync(cancellationToken);
            await _httpServer.StopAsync(cancellationToken);
            await ((IHostedService)_udpServer).StopAsync(cancellationToken);
            await ((IHostedService)_scriptHost).StopAsync(cancellationToken);
            await _performanceMonitor.StopAsync(cancellationToken);

            _logger.LogInformation("Server Application stopped.");
        }
    }
}
