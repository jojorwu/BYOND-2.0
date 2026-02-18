using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Interfaces;
using Shared.Services;

namespace Server
{
    public class PerformanceMonitor : EngineService, IHostedService, IDisposable
    {
        public override int Priority => 100; // High priority

        private readonly ILogger<PerformanceMonitor> _logger;
        private readonly IProfilingService? _profilingService;
        private Timer? _timer;
        private long _tickCount;
        private long _errorCount;
        private long _bytesSent;
        private long _bytesReceived;
        private double _lastTps;

        public double LastTps => _lastTps;

        public PerformanceMonitor(ILogger<PerformanceMonitor> logger, IProfilingService? profilingService = null)
        {
            _logger = logger;
            _profilingService = profilingService;
        }

        public void RecordTick() => Interlocked.Increment(ref _tickCount);
        public void RecordError() => Interlocked.Increment(ref _errorCount);
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
            var errors = Interlocked.Exchange(ref _errorCount, 0);
            var sent = Interlocked.Exchange(ref _bytesSent, 0);
            var received = Interlocked.Exchange(ref _bytesReceived, 0);

            double tps = ticks / 5.0;
            _lastTps = tps;
            string errorInfo = errors > 0 ? $" | ERRORS: {errors}" : "";
            _logger.LogInformation($"Performance: {tps:F1} TPS{errorInfo} | Sent: {sent / 1024.0:F1} KB/s | Received: {received / 1024.0:F1} KB/s");

            var process = Process.GetCurrentProcess();
            var workingSet = process.WorkingSet64;
            var privateBytes = process.PrivateMemorySize64;
            var gcMem = GC.GetTotalMemory(false);

            _logger.LogInformation($"Memory: WorkingSet: {workingSet / 1024.0 / 1024.0:F1} MB | Private: {privateBytes / 1024.0 / 1024.0:F1} MB | GC: {gcMem / 1024.0 / 1024.0:F1} MB");
            _logger.LogInformation($"GC Collections: G0: {GC.CollectionCount(0)}, G1: {GC.CollectionCount(1)}, G2: {GC.CollectionCount(2)}");

            if (_profilingService != null)
            {
                var summaries = _profilingService.GetSummaries();
                var topSystems = summaries.Where(s => s.Key.StartsWith("System."))
                    .OrderByDescending(s => s.Value.Average)
                    .Take(5);

                if (topSystems.Any())
                {
                    _logger.LogInformation("Top 5 slowest systems (avg ms):");
                    foreach (var sys in topSystems)
                    {
                        _logger.LogInformation($"  - {sys.Key.Replace("System.", "")}: {sys.Value.Average:F3} ms (max: {sys.Value.Max:F3} ms)");
                    }
                }
            }
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
