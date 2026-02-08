using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared;
using Shared.Services;

namespace Server
{
    public class HttpServer : EngineService, IHostedService
    {
        public override int Priority => -50; // Moderate priority
        private readonly IHost? _host;
        private readonly ILogger<HttpServer> _logger;

        public HttpServer(IOptions<ServerSettings> settingsOptions, IProject project, ILogger<HttpServer> logger)
        {
            _logger = logger;
            var settings = settingsOptions.Value;
            if (!settings.HttpServer.Enabled)
            {
                return;
            }

            var assetsPath = project.GetFullPath(settings.HttpServer.AssetsRoot);
            if (!Directory.Exists(assetsPath))
            {
                _logger.LogWarning($"Assets directory '{assetsPath}' not found. Creating it.");
                Directory.CreateDirectory(assetsPath);
            }

            _host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel()
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
                        });
                })
                .Build();
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_host != null)
            {
                _logger.LogInformation("Starting HTTP server...");
                await _host.StartAsync(cancellationToken);
                _logger.LogInformation("HTTP server started.");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_host != null)
            {
                _logger.LogInformation("Stopping HTTP server...");
                await _host.StopAsync(cancellationToken);
                _logger.LogInformation("HTTP server stopped.");
            }
        }
    }
}
