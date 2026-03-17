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
/// A reusable diagnostic event to avoid allocations.
/// </summary>
public class DiagnosticEvent
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
}

/// <summary>
/// A specialized bus for high-performance publishing of diagnostic information and metrics.
/// </summary>
public interface IDiagnosticBus
{
    void Publish(string source, string message, DiagnosticSeverity severity = DiagnosticSeverity.Info, Action<Dictionary<string, object>>? metricsAction = null);
        void Publish<TState>(string source, string message, TState state, Action<Dictionary<string, object>, TState> metricsAction, DiagnosticSeverity severity = DiagnosticSeverity.Info);
    IDisposable Subscribe(Action<DiagnosticEvent> callback);
}
