using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Services;

public class DiagnosticBus : IDiagnosticBus
{
    private volatile Action<DiagnosticEvent>[] _subscribers = Array.Empty<Action<DiagnosticEvent>>();
    private readonly object _lock = new();

    public void Publish(DiagnosticEvent diagnosticEvent)
    {
        var subscribers = _subscribers;
        for (int i = 0; i < subscribers.Length; i++)
        {
            try
            {
                subscribers[i](diagnosticEvent);
            }
            catch { /* Diagnostics should never throw */ }
        }
    }

    public IDisposable Subscribe(Action<DiagnosticEvent> callback)
    {
        lock (_lock)
        {
            var updated = new Action<DiagnosticEvent>[_subscribers.Length + 1];
            Array.Copy(_subscribers, updated, _subscribers.Length);
            updated[_subscribers.Length] = callback;
            _subscribers = updated;
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
                int index = Array.IndexOf(_bus._subscribers, _callback);
                if (index == -1) return;

                var updated = new Action<DiagnosticEvent>[_bus._subscribers.Length - 1];
                if (index > 0) Array.Copy(_bus._subscribers, 0, updated, 0, index);
                if (index < _bus._subscribers.Length - 1) Array.Copy(_bus._subscribers, index + 1, updated, index, _bus._subscribers.Length - index - 1);
                _bus._subscribers = updated;
            }
        }
    }
}
