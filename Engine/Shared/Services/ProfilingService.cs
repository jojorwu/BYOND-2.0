using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Shared.Interfaces;

namespace Shared.Services;
    public class ProfilingService : IProfilingService, IShrinkable
    {
        private readonly ConcurrentDictionary<string, MetricData> _metrics = new();
        private const int MaxSamples = 100;
        private const int MaxUniqueMetrics = 1000;

        [ThreadStatic]
        private static Stack<MeasureScope>? _scopeStack;

        private class MetricData
        {
            public readonly ConcurrentQueue<double> Samples = new();
            public readonly ConcurrentDictionary<string, MetricData> Children = new();
        }

        public void RecordMetric(string name, double value)
        {
            var currentScope = _scopeStack?.Count > 0 ? _scopeStack.Peek() : null;
            MetricData? metric;

            if (currentScope != null)
            {
                if (currentScope.Metric.Children.Count >= MaxUniqueMetrics && !currentScope.Metric.Children.ContainsKey(name)) return;
                metric = currentScope.Metric.Children.GetOrAdd(name, _ => new MetricData());
            }
            else
            {
                if (_metrics.Count >= MaxUniqueMetrics && !_metrics.ContainsKey(name)) return;
                metric = _metrics.GetOrAdd(name, _ => new MetricData());
            }

            metric.Samples.Enqueue(value);
            while (metric.Samples.Count > MaxSamples)
            {
                metric.Samples.TryDequeue(out _);
            }
        }

        private static readonly NullMeasureScope _nullScope = new();

        public IDisposable Measure(string name)
        {
            _scopeStack ??= new Stack<MeasureScope>();
            var parent = _scopeStack.Count > 0 ? _scopeStack.Peek() : null;

            MetricData? metric;
            if (parent != null)
            {
                if (parent.Metric.Children.Count >= MaxUniqueMetrics && !parent.Metric.Children.ContainsKey(name))
                    return _nullScope;
                metric = parent.Metric.Children.GetOrAdd(name, _ => new MetricData());
            }
            else
            {
                if (_metrics.Count >= MaxUniqueMetrics && !_metrics.ContainsKey(name))
                    return _nullScope;
                metric = _metrics.GetOrAdd(name, _ => new MetricData());
            }

            var scope = RentScope(name, metric);
            _scopeStack.Push(scope);
            return scope;
        }

        private MeasureScope RentScope(string name, MetricData metric)
        {
            _scopePool ??= new Stack<MeasureScope>();
            if (_scopePool.TryPop(out var scope))
            {
                scope.Initialize(name, metric);
                return scope;
            }
            return new MeasureScope(this, name, metric);
        }

        private void ReturnScope(MeasureScope scope)
        {
            _scopePool?.Push(scope);
        }

        [ThreadStatic]
        private static Stack<MeasureScope>? _scopePool;

        public IReadOnlyDictionary<string, MetricSummary> GetSummaries()
        {
            var results = new Dictionary<string, MetricSummary>();
            foreach (var kvp in _metrics)
            {
                SummarizeRecursively(kvp.Key, kvp.Value, "", results);
            }
            return results;
        }

        private void SummarizeRecursively(string name, MetricData data, string prefix, Dictionary<string, MetricSummary> results)
        {
            string fullName = string.IsNullOrEmpty(prefix) ? name : $"{prefix}/{name}";
            var samples = data.Samples.ToArray();

            if (samples.Length > 0)
            {
                results[fullName] = new MetricSummary(
                    samples.Average(),
                    samples.Min(),
                    samples.Max(),
                    samples.Length
                );
            }

            foreach (var child in data.Children)
            {
                SummarizeRecursively(child.Key, child.Value, fullName, results);
            }
        }

        private class NullMeasureScope : IDisposable
        {
            public void Dispose() { }
        }

        private class MeasureScope : IDisposable
        {
            private readonly ProfilingService _service;
            private string _name;
            public MetricData Metric;
            private long _startTimestamp;

            public MeasureScope(ProfilingService service, string name, MetricData metric)
            {
                _service = service;
                _name = name;
                Metric = metric;
                _startTimestamp = Stopwatch.GetTimestamp();
            }

            public void Initialize(string name, MetricData metric)
            {
                _name = name;
                Metric = metric;
                _startTimestamp = Stopwatch.GetTimestamp();
            }

            public void Dispose()
            {
                long endTimestamp = Stopwatch.GetTimestamp();
                double duration = (endTimestamp - _startTimestamp) * 1000.0 / Stopwatch.Frequency;

                _service.RecordMetricInternal(Metric, duration);

                if (_scopeStack != null && _scopeStack.Count > 0 && _scopeStack.Peek() == this)
                {
                    _scopeStack.Pop();
                }

                _service.ReturnScope(this);
            }
        }

        private void RecordMetricInternal(MetricData metric, double value)
        {
            metric.Samples.Enqueue(value);
            while (metric.Samples.Count > MaxSamples)
            {
                metric.Samples.TryDequeue(out _);
            }
        }

        public void Shrink()
        {
            if (_metrics.Count > MaxUniqueMetrics)
            {
                _metrics.Clear();
            }
        }
    }
