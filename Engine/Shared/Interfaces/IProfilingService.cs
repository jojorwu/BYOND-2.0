using System;
using System.Collections.Generic;

namespace Shared.Interfaces;
    public interface IProfilingService
    {
        void RecordMetric(string name, double value);
        IDisposable Measure(string name);
        IReadOnlyDictionary<string, MetricSummary> GetSummaries();
    }

    public record MetricSummary(double Average, double Min, double Max, long Count);
