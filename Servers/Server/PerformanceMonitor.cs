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
        private readonly Stopwatch _stopwatch = new();
        private long _tickCount;
        private long _bytesSent;
        private long _bytesReceived;
        private long _errorCount;

        public double LastTps { get; private set; }
        public long CumulativeErrors => Interlocked.Read(ref _errorCount);

        public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
        {
            _logger = logger;
        }

        public void RecordTick() => Interlocked.Increment(ref _tickCount);
        public void RecordBytesSent(long bytes) => Interlocked.Add(ref _bytesSent, bytes);
        public void RecordBytesReceived(long bytes) => Interlocked.Add(ref _bytesReceived, bytes);
        public void RecordError() => Interlocked.Increment(ref _errorCount);

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _stopwatch.Start();
            _timer = new Timer(LogStats, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            return Task.CompletedTask;
        }

        private void LogStats(object? state)
        {
            double elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
            _stopwatch.Restart();

            var ticks = Interlocked.Exchange(ref _tickCount, 0);
            var sent = Interlocked.Exchange(ref _bytesSent, 0);
            var received = Interlocked.Exchange(ref _bytesReceived, 0);

            LastTps = elapsedSeconds > 0 ? ticks / elapsedSeconds : 0;
            _logger.LogInformation($"Performance: {LastTps:F1} TPS | Sent: {sent / elapsedSeconds / 1024.0:F1} KB/s | Received: {received / elapsedSeconds / 1024.0:F1} KB/s");

            var workingSet = Process.GetCurrentProcess().WorkingSet64;
            _logger.LogInformation($"Memory: {workingSet / 1024.0 / 1024.0:F1} MB | GC0: {GC.CollectionCount(0)} | GC1: {GC.CollectionCount(1)} | GC2: {GC.CollectionCount(2)}");

            if (CumulativeErrors > 0)
            {
                _logger.LogWarning($"Stability: {CumulativeErrors} total errors recorded.");
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            _stopwatch.Stop();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
