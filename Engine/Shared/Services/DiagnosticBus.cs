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

    private long _totalPublished;
    private long _eventsDropped;
    private long _totalDispatched;
    private long _lastProcessDurationMs;

    public DiagnosticBus()
    {
        _eventChannel = Channel.CreateUnbounded<DiagnosticEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        _backgroundWorker = Task.Run(() => ProcessEventsAsync(_cts.Token), cancellationToken);
        return Task.CompletedTask;
    }

    public void Publish(string source, string message, DiagnosticSeverity severity = DiagnosticSeverity.Info, Action<IMetricsBuilder>? metricsAction = null, string[]? tags = null)
    {
        Interlocked.Increment(ref _totalPublished);
        if (_subscribers.Length == 0 && _poolCount > MaxPoolSize / 2) {
            Interlocked.Increment(ref _eventsDropped);
            return;
        }

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
        ev.Tags = tags;
        metricsAction?.Invoke(ev);

        if (!_eventChannel.Writer.TryWrite(ev))
        {
            Interlocked.Increment(ref _eventsDropped);
            ReturnToPool(ev);
        }
    }

    public void Publish<TState>(string source, string message, TState state, Action<IMetricsBuilder, TState> metricsAction, DiagnosticSeverity severity = DiagnosticSeverity.Info, string[]? tags = null)
    {
        Interlocked.Increment(ref _totalPublished);
        if (_subscribers.Length == 0 && _poolCount > MaxPoolSize / 2) {
            Interlocked.Increment(ref _eventsDropped);
            return;
        }

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
        ev.Tags = tags;
        metricsAction(ev, state);

        if (!_eventChannel.Writer.TryWrite(ev))
        {
            Interlocked.Increment(ref _eventsDropped);
            ReturnToPool(ev);
        }
    }

    private async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        var reader = _eventChannel.Reader;
        const int BatchSize = 32;
        var batch = new DiagnosticEvent[BatchSize];
        var sw = new System.Diagnostics.Stopwatch();

        try
        {
            // Use WaitToReadAsync for energy-efficient non-blocking wait
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                sw.Restart();
                // Optimized Batched Dispatch:
                // Draining multiple events into a local buffer before notifying subscribers
                // reduces the frequency of volatile reads of the subscribers list and overhead.
                int count = 0;
                while (count < BatchSize && reader.TryRead(out var ev))
                {
                    batch[count++] = ev;
                }

                if (count > 0)
                {
                    var subscribers = _subscribers;
                    if (subscribers.Length > 0)
                    {
                        for (int j = 0; j < count; j++)
                        {
                            var ev = batch[j];
                            for (int i = 0; i < subscribers.Length; i++)
                            {
                                try
                                {
                                    subscribers[i](ev);
                                }
                                catch { /* Diagnostics should never throw */ }
                            }
                            Interlocked.Increment(ref _totalDispatched);
                        }
                    }

                    for (int j = 0; j < count; j++)
                    {
                        ReturnToPool(batch[j]);
                        batch[j] = null!;
                    }
                }
                sw.Stop();
                Volatile.Write(ref _lastProcessDurationMs, sw.ElapsedMilliseconds);
            }
        }
        catch (OperationCanceledException) { /* Normal shutdown */ }
        catch (ChannelClosedException) { /* Normal shutdown */ }
        catch (Exception ex)
        {
            // Log unexpected worker errors but don't crash
            Console.WriteLine($"DiagnosticBus worker error: {ex}");
        }
        finally
        {
            // Drain remaining events if any to ensure pool maintenance
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

        using (_lock.EnterScope())
        {
            _subscribers = Array.Empty<Action<DiagnosticEvent>>();
        }
        _pool.Clear();
        _poolCount = 0;
        await base.StopAsync(cancellationToken);
    }

    public IDisposable Subscribe(Action<DiagnosticEvent> callback)
    {
        using (_lock.EnterScope())
        {
            var updated = new Action<DiagnosticEvent>[_subscribers.Length + 1];
            Array.Copy(_subscribers, updated, _subscribers.Length);
            updated[_subscribers.Length] = callback;
            _subscribers = updated;
        }
        return new Unsubscriber(this, callback);
    }

    public override Dictionary<string, object> GetDiagnosticInfo()
    {
        var info = base.GetDiagnosticInfo();
        info["TotalPublished"] = Interlocked.Read(ref _totalPublished);
        info["EventsDropped"] = Interlocked.Read(ref _eventsDropped);
        info["TotalDispatched"] = Interlocked.Read(ref _totalDispatched);
        info["PoolCount"] = _poolCount;
        info["LastProcessDurationMs"] = Volatile.Read(ref _lastProcessDurationMs);
        info["SubscriberCount"] = _subscribers.Length;
        return info;
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
            using (_bus._lock.EnterScope())
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
