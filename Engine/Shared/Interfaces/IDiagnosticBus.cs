using System;
using System.Collections.Generic;

namespace Shared.Interfaces;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Provides a way to add metrics to a diagnostic event without direct dictionary access.
/// </summary>
public interface IMetricsBuilder
{
    IMetricsBuilder Add(string name, object value);
    IMetricsBuilder Add(string name, long value);
    IMetricsBuilder Add(string name, double value);
    IMetricsBuilder Add(string name, string value);
}

/// <summary>
/// A reusable diagnostic event to avoid allocations.
/// </summary>
public sealed class DiagnosticEvent : IMetricsBuilder
{
    public string Source { get; internal set; } = string.Empty;
    public string Message { get; internal set; } = string.Empty;
    public DiagnosticSeverity Severity { get; internal set; } = DiagnosticSeverity.Info;
    public string[]? Tags { get; internal set; }

    private readonly Dictionary<string, object> _metrics = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, object> Metrics => _metrics;

    internal void Clear()
    {
        Source = string.Empty;
        Message = string.Empty;
        Severity = DiagnosticSeverity.Info;
        _metrics.Clear();
    }

    public IMetricsBuilder Add(string name, object value) { _metrics[name] = value; return this; }
    public IMetricsBuilder Add(string name, long value) { _metrics[name] = value; return this; }
    public IMetricsBuilder Add(string name, double value) { _metrics[name] = value; return this; }
    public IMetricsBuilder Add(string name, string value) { _metrics[name] = value; return this; }
}

/// <summary>
/// A specialized bus for high-performance publishing of diagnostic information and metrics.
/// </summary>
public interface IDiagnosticBus
{
    void Publish(string source, string message, DiagnosticSeverity severity = DiagnosticSeverity.Info, Action<IMetricsBuilder>? metricsAction = null, string[]? tags = null);
    void Publish<TState>(string source, string message, TState state, Action<IMetricsBuilder, TState> metricsAction, DiagnosticSeverity severity = DiagnosticSeverity.Info, string[]? tags = null);
    IDisposable Subscribe(Action<DiagnosticEvent> callback);
    void SetThreshold(string metricName, double warningValue, double criticalValue);
}
