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
}

/// <summary>
/// A reusable diagnostic event to avoid allocations.
/// </summary>
public class DiagnosticEvent : IMetricsBuilder
{
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DiagnosticSeverity Severity { get; set; } = DiagnosticSeverity.Info;
    public Dictionary<string, object> Metrics { get; } = new();

    public void Clear()
    {
        Source = string.Empty;
        Message = string.Empty;
        Severity = DiagnosticSeverity.Info;
        Metrics.Clear();
    }

    public IMetricsBuilder Add(string name, object value) { Metrics[name] = value; return this; }
    public IMetricsBuilder Add(string name, long value) { Metrics[name] = value; return this; }
    public IMetricsBuilder Add(string name, double value) { Metrics[name] = value; return this; }
}

/// <summary>
/// A specialized bus for high-performance publishing of diagnostic information and metrics.
/// </summary>
public interface IDiagnosticBus
{
    void Publish(string source, string message, DiagnosticSeverity severity = DiagnosticSeverity.Info, Action<IMetricsBuilder>? metricsAction = null);
    void Publish<TState>(string source, string message, TState state, Action<IMetricsBuilder, TState> metricsAction, DiagnosticSeverity severity = DiagnosticSeverity.Info);
    IDisposable Subscribe(Action<DiagnosticEvent> callback);
}
