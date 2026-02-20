using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Services;

namespace Server
{
    public class PerformanceMonitor : EngineService, IHostedService, IDisposable
    {
        public override int Priority => 100; // High priority

        private readonly ILogger<PerformanceMonitor> _logger;
        private Timer? _timer;
        private long _tickCount;
        private long _bytesSent;
        private long _bytesReceived;

        public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
        {
            _logger = logger;
        }

        public void RecordTick() => Interlocked.Increment(ref _tickCount);
        public void RecordBytesSent(long bytes) => Interlocked.Add(ref _bytesSent, bytes);
        public void RecordBytesReceived(long bytes) => Interlocked.Add(ref _bytesReceived, bytes);

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(LogStats, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            return Task.CompletedTask;
        }

        private void LogStats(object? state)
        {
            var ticks = Interlocked.Exchange(ref _tickCount, 0);
            var sent = Interlocked.Exchange(ref _bytesSent, 0);
            var received = Interlocked.Exchange(ref _bytesReceived, 0);

            double tps = ticks / 5.0;
            _logger.LogInformation($"Performance: {tps:F1} TPS | Sent: {sent / 1024.0:F1} KB/s | Received: {received / 1024.0:F1} KB/s");

            var workingSet = Process.GetCurrentProcess().WorkingSet64;
            _logger.LogInformation($"Memory: {workingSet / 1024.0 / 1024.0:F1} MB");
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
