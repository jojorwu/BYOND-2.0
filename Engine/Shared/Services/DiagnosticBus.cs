using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Shared.Interfaces;

namespace Shared.Services;

public class DiagnosticBus : IDiagnosticBus
{
    private volatile Action<DiagnosticEvent>[] _subscribers = Array.Empty<Action<DiagnosticEvent>>();
    private readonly object _lock = new();

    // Pool of DiagnosticEvent objects to eliminate allocations on Publish
    private readonly ConcurrentStack<DiagnosticEvent> _pool = new();
    private volatile int _poolCount;
    private const int MaxPoolSize = 1024;

    public void Publish(string source, string message, DiagnosticSeverity severity = DiagnosticSeverity.Info, Action<Dictionary<string, object>>? metricsAction = null)
    {
        var subscribers = _subscribers;
        if (subscribers.Length == 0) return;

        if (!_pool.TryPop(out var ev))
        {
            ev = new DiagnosticEvent();
        }
        else
        {
            Interlocked.Decrement(ref _poolCount);
        }

        try
        {
            ev.Source = source;
            ev.Message = message;
            ev.Severity = severity;
            metricsAction?.Invoke(ev.Metrics);

            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    subscribers[i](ev);
                }
                catch { /* Diagnostics should never throw */ }
            }
        }
        finally
        {
            ev.Clear();
            if (_poolCount < MaxPoolSize)
            {
                _pool.Push(ev);
                Interlocked.Increment(ref _poolCount);
            }
        }
    }

    public void Publish<TState>(string source, string message, TState state, Action<Dictionary<string, object>, TState> metricsAction, DiagnosticSeverity severity = DiagnosticSeverity.Info)
    {
        var subscribers = _subscribers;
        if (subscribers.Length == 0) return;

        if (!_pool.TryPop(out var ev))
        {
            ev = new DiagnosticEvent();
        }
        else
        {
            Interlocked.Decrement(ref _poolCount);
        }

        try
        {
            ev.Source = source;
            ev.Message = message;
            ev.Severity = severity;
            metricsAction(ev.Metrics, state);

            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    subscribers[i](ev);
                }
                catch { /* Diagnostics should never throw */ }
            }
        }
        finally
        {
            ev.Clear();
            if (_poolCount < MaxPoolSize)
            {
                _pool.Push(ev);
                Interlocked.Increment(ref _poolCount);
            }
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
