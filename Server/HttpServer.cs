using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;

namespace Server
{
    public class HttpServer : IHostedService
    {
        private readonly IWebHost _host;
        private readonly ILogger<HttpServer> _logger;

        public HttpServer(ServerSettings settings, IProject project, ILogger<HttpServer> logger)
        {
            _logger = logger;
            if (!settings.HttpServer.Enabled)
            {
                _host = new WebHostBuilder().Build(); // Create a dummy host
                return;
            }

            var assetsPath = project.GetFullPath(settings.HttpServer.AssetsRoot);
            if (!Directory.Exists(assetsPath))
            {
                _logger.LogWarning($"Assets directory '{assetsPath}' not found. Creating it.");
                Directory.CreateDirectory(assetsPath);
            }

            _host = new WebHostBuilder()
                .UseKestrel()
                .UseUrls($"http://*:{settings.HttpServer.Port}")
                .ConfigureServices(services =>
                {
                    services.AddDirectoryBrowser();
                })
                .Configure(app =>
                {
                    app.UseFileServer(new FileServerOptions
                    {
                        FileProvider = new PhysicalFileProvider(assetsPath),
                        RequestPath = "",
                        EnableDirectoryBrowsing = true
                    });
                })
                .Build();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_host.Services != null) // Check if the host is not a dummy
            {
                _logger.LogInformation("Starting HTTP server...");
                await _host.StartAsync(cancellationToken);
                _logger.LogInformation("HTTP server started.");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_host.Services != null)
            {
                _logger.LogInformation("Stopping HTTP server...");
                await _host.StopAsync(cancellationToken);
                _logger.LogInformation("HTTP server stopped.");
            }
        }
    }
}
