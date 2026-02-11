using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Shared.Interfaces;

namespace Shared.Services
{
    public class ProfilingService : IProfilingService
    {
        private readonly ConcurrentDictionary<string, ConcurrentQueue<double>> _metrics = new();
        private const int MaxSamples = 100;

        public void RecordMetric(string name, double value)
        {
            var queue = _metrics.GetOrAdd(name, _ => new ConcurrentQueue<double>());
            queue.Enqueue(value);

            while (queue.Count > MaxSamples)
            {
                queue.TryDequeue(out _);
            }
        }

        public IDisposable Measure(string name)
        {
            return new MeasureScope(this, name);
        }

        public IReadOnlyDictionary<string, MetricSummary> GetSummaries()
        {
            return _metrics.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    var samples = kvp.Value.ToArray();
                    if (samples.Length == 0) return new MetricSummary(0, 0, 0, 0);
                    return new MetricSummary(
                        samples.Average(),
                        samples.Min(),
                        samples.Max(),
                        samples.Length
                    );
                });
        }

        private class MeasureScope : IDisposable
        {
            private readonly ProfilingService _service;
            private readonly string _name;
            private readonly Stopwatch _stopwatch;

            public MeasureScope(ProfilingService service, string name)
            {
                _service = service;
                _name = name;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                _service.RecordMetric(_name, _stopwatch.Elapsed.TotalMilliseconds);
            }
        }
    }
}
