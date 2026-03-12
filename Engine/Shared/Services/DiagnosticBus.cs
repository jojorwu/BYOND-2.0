using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Services;

public class DiagnosticBus : IDiagnosticBus
{
    private readonly List<Action<DiagnosticEvent>> _subscribers = new();
    private readonly object _lock = new();

    public void Publish(DiagnosticEvent diagnosticEvent)
    {
        Action<DiagnosticEvent>[] subscribers;
        lock (_lock)
        {
            subscribers = _subscribers.ToArray();
        }

        foreach (var sub in subscribers)
        {
            try
            {
                sub(diagnosticEvent);
            }
            catch { /* Diagnostics should never throw */ }
        }
    }

    public IDisposable Subscribe(Action<DiagnosticEvent> callback)
    {
        lock (_lock)
        {
            _subscribers.Add(callback);
        }
        return new Unsubscriber(this, callback);
    }

    private class Unsubscriber : IDisposable
    {
        private readonly DiagnosticBus _bus;
        private readonly Action<DiagnosticEvent> _callback;

        public Unsubscriber(DiagnosticBus bus, Action<DiagnosticEvent> callback)
        {
            _bus = bus;
            _callback = callback;
        }

        public void Dispose()
        {
            lock (_bus._lock)
            {
                _bus._subscribers.Remove(_callback);
            }
        }
    }
}
