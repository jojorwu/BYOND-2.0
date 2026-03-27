using System;
using Shared.Interfaces;

namespace Shared.Services;

public class MockDiagnosticBus : IDiagnosticBus
{
    public static readonly MockDiagnosticBus Instance = new();

    public void Publish(string source, string message, DiagnosticSeverity severity = DiagnosticSeverity.Info, Action<IMetricsBuilder>? metricsAction = null) { }
    public void Publish<TState>(string source, string message, TState state, Action<IMetricsBuilder, TState> metricsAction, DiagnosticSeverity severity = DiagnosticSeverity.Info) { }
    public IDisposable Subscribe(Action<DiagnosticEvent> callback) => new Unsubscriber();
    private class Unsubscriber : IDisposable { public void Dispose() { } }
}
