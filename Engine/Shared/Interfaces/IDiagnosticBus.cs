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

public record DiagnosticEvent(string Source, string Message, DiagnosticSeverity Severity, Dictionary<string, object>? Metrics = null);

/// <summary>
/// A specialized bus for high-performance publishing of diagnostic information and metrics.
/// </summary>
public interface IDiagnosticBus
{
    void Publish(DiagnosticEvent diagnosticEvent);
    IDisposable Subscribe(Action<DiagnosticEvent> callback);
}
