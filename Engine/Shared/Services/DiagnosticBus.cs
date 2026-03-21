using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Services;

public class DiagnosticBus : EngineService, IDiagnosticBus
{
    private volatile Action<DiagnosticEvent>[] _subscribers = Array.Empty<Action<DiagnosticEvent>>();
    private readonly System.Threading.Lock _lock = new();

    private readonly Channel<DiagnosticEvent> _eventChannel;
    private readonly CancellationTokenSource _cts = new();
    private Task? _backgroundWorker;

    // Pool of DiagnosticEvent objects to eliminate allocations on Publish
    private readonly ConcurrentStack<DiagnosticEvent> _pool = new();
    private volatile int _poolCount;
    private const int MaxPoolSize = 1024;

    public DiagnosticBus()
    {
        _eventChannel = Channel.CreateUnbounded<DiagnosticEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _backgroundWorker = Task.Run(() => ProcessEventsAsync(_cts.Token), cancellationToken);
        return base.StartAsync(cancellationToken);
    }

    public void Publish(string source, string message, DiagnosticSeverity severity = DiagnosticSeverity.Info, Action<IMetricsBuilder>? metricsAction = null)
    {
        if (_subscribers.Length == 0) return;

        DiagnosticEvent? ev;
        if (!_pool.TryPop(out ev))
        {
            ev = new DiagnosticEvent();
        }
        else
        {
            Interlocked.Decrement(ref _poolCount);
        }

        ev.Source = source;
        ev.Message = message;
        ev.Severity = severity;
        metricsAction?.Invoke(ev);

        if (!_eventChannel.Writer.TryWrite(ev))
        {
            ReturnToPool(ev);
        }
    }

    public void Publish<TState>(string source, string message, TState state, Action<IMetricsBuilder, TState> metricsAction, DiagnosticSeverity severity = DiagnosticSeverity.Info)
    {
        if (_subscribers.Length == 0) return;

        DiagnosticEvent? ev;
        if (!_pool.TryPop(out ev))
        {
            ev = new DiagnosticEvent();
        }
        else
        {
            Interlocked.Decrement(ref _poolCount);
        }

        ev.Source = source;
        ev.Message = message;
        ev.Severity = severity;
        metricsAction(ev, state);

        if (!_eventChannel.Writer.TryWrite(ev))
        {
            ReturnToPool(ev);
        }
    }

    private async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        var reader = _eventChannel.Reader;
        try
        {
            while (await reader.WaitToReadAsync(cancellationToken))
            {
                while (reader.TryRead(out var ev))
                {
                    var subscribers = _subscribers;
                    for (int i = 0; i < subscribers.Length; i++)
                    {
                        try
                        {
                            subscribers[i](ev);
                        }
                        catch { /* Diagnostics should never throw */ }
                    }
                    ReturnToPool(ev);
                }
            }
        }
        catch (OperationCanceledException) { /* Normal shutdown */ }
        catch (ChannelClosedException) { /* Normal shutdown */ }
        finally
        {
            // Drain remaining events if any
            while (reader.TryRead(out var ev))
            {
                ReturnToPool(ev);
            }
        }
    }

    private void ReturnToPool(DiagnosticEvent ev)
    {
        ev.Clear();
        if (_poolCount < MaxPoolSize)
        {
            _pool.Push(ev);
            Interlocked.Increment(ref _poolCount);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        _eventChannel.Writer.Complete();

        if (_backgroundWorker != null)
        {
            await Task.WhenAny(_backgroundWorker, Task.Delay(1000, cancellationToken));
        }

        lock (_lock)
        {
            _subscribers = Array.Empty<Action<DiagnosticEvent>>();
        }
        _pool.Clear();
        _poolCount = 0;
        await base.StopAsync(cancellationToken);
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
