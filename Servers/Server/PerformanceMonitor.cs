using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Services;
using Shared.Interfaces;

namespace Server
{
    public class PerformanceMonitor : EngineService, IHostedService, IDisposable
    {
        public override int Priority => 100; // High priority

        private readonly ILogger<PerformanceMonitor> _logger;
        private readonly IProfilingService? _profilingService;
        private readonly IDiagnosticBus _diagnosticBus;
        private Timer? _timer;
        private readonly Stopwatch _stopwatch = new();
        private long _tickCount;
        private long _bytesSent;
        private long _bytesReceived;
        private long _errorCount;

        public double LastTps { get; private set; }
        public long CumulativeErrors => Interlocked.Read(ref _errorCount);

        public PerformanceMonitor(ILogger<PerformanceMonitor> logger, IDiagnosticBus diagnosticBus, IProfilingService? profilingService = null)
        {
            _logger = logger;
            _diagnosticBus = diagnosticBus;
            _profilingService = profilingService;
        }

        public void RecordTick() => Interlocked.Increment(ref _tickCount);
        public void RecordBytesSent(long bytes) => Interlocked.Add(ref _bytesSent, bytes);
        public void RecordBytesReceived(long bytes) => Interlocked.Add(ref _bytesReceived, bytes);
        public void RecordError() => Interlocked.Increment(ref _errorCount);

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            _stopwatch.Start();
            _timer = new Timer(LogStats, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            return Task.CompletedTask;
        }

        private void LogStats(object? state)
        {
            double elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
            if (elapsedSeconds <= 0) return;
            _stopwatch.Restart();

            var ticks = Interlocked.Exchange(ref _tickCount, 0);
            var sent = Interlocked.Exchange(ref _bytesSent, 0);
            var received = Interlocked.Exchange(ref _bytesReceived, 0);

            LastTps = ticks / elapsedSeconds;
            var workingSet = Process.GetCurrentProcess().WorkingSet64;

            _diagnosticBus.Publish("PerformanceMonitor", "Telemetry Update", DiagnosticSeverity.Info, m =>
            {
                m.Add("TPS", LastTps);
                m.Add("SentKBps", sent / elapsedSeconds / 1024.0);
                m.Add("ReceivedKBps", received / elapsedSeconds / 1024.0);
                m.Add("WorkingSetMB", workingSet / 1024.0 / 1024.0);
                m.Add("GC0", GC.CollectionCount(0));
                m.Add("GC1", GC.CollectionCount(1));
                m.Add("GC2", GC.CollectionCount(2));
                m.Add("Errors", CumulativeErrors);
            });

            _logger.LogTrace("Telemetry published: {TPS:F1} TPS, {WS:F1} MB", LastTps, workingSet / 1024.0 / 1024.0);

            if (_profilingService != null)
            {
                var summaries = _profilingService.GetSummaries();
                var phaseKeys = new[] { "SystemManager.Phase.Input", "SystemManager.Phase.Simulation", "SystemManager.Phase.LateUpdate", "SystemManager.Phase.Render", "SystemManager.Phase.Cleanup" };
                foreach (var key in phaseKeys)
                {
                    if (summaries.TryGetValue(key, out var summary))
                    {
                        _diagnosticBus.Publish("PerformanceMonitor.Profiling", $"Phase {key.Split('.').Last()}", DiagnosticSeverity.Info, m =>
                        {
                            m.Add("Phase", key);
                            m.Add("AvgMs", summary.Average);
                            m.Add("MaxMs", summary.Max);
                        });
                    }
                }
            }
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
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
