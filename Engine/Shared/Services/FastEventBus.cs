using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Messaging;

namespace Shared.Services;

public class FastEventBus : EngineService, IEventBus, IFreezable
{
    private readonly IDiagnosticBus _diagnosticBus;
    private long _totalPublished;
    private long _totalAsyncPublished;

    public FastEventBus(IDiagnosticBus diagnosticBus)
    {
        _diagnosticBus = diagnosticBus;
    }

    private interface IHandlerList
    {
        void Publish(object eventData);
        void PublishRef<TEvent>(in TEvent eventData) where TEvent : struct;
        ValueTask PublishAsync(object eventData);
        void Unsubscribe(object handler);
        int Count { get; }
    }

    private record struct HandlerEntry<TDelegate>(TDelegate Delegate, int Priority);

    private class HandlerList<T> : IHandlerList
    {
        private volatile HandlerEntry<Action<T>>[] _actions = Array.Empty<HandlerEntry<Action<T>>>();
        private volatile HandlerEntry<Func<T, ValueTask>>[] _asyncActions = Array.Empty<HandlerEntry<Func<T, ValueTask>>>();
        private volatile HandlerEntry<IEventHandler<T>>[] _interfaceHandlers = Array.Empty<HandlerEntry<IEventHandler<T>>>();
        private readonly System.Threading.Lock _lock = new();

        public int Count => _actions.Length + _asyncActions.Length + _interfaceHandlers.Length;

        public void Subscribe(Action<T> handler, int priority)
        {
            using (_lock.EnterScope())
            {
                var updated = new HandlerEntry<Action<T>>[_actions.Length + 1];
                Array.Copy(_actions, updated, _actions.Length);
                updated[_actions.Length] = new HandlerEntry<Action<T>>(handler, priority);
                Array.Sort(updated, (a, b) => b.Priority.CompareTo(a.Priority)); // Higher priority first
                _actions = updated;
            }
        }

        public void Subscribe(Func<T, ValueTask> handler, int priority)
        {
            using (_lock.EnterScope())
            {
                var updated = new HandlerEntry<Func<T, ValueTask>>[_asyncActions.Length + 1];
                Array.Copy(_asyncActions, updated, _asyncActions.Length);
                updated[_asyncActions.Length] = new HandlerEntry<Func<T, ValueTask>>(handler, priority);
                Array.Sort(updated, (a, b) => b.Priority.CompareTo(a.Priority)); // Higher priority first
                _asyncActions = updated;
            }
        }

        public void Subscribe(IEventHandler<T> handler, int priority)
        {
            using (_lock.EnterScope())
            {
                var updated = new HandlerEntry<IEventHandler<T>>[_interfaceHandlers.Length + 1];
                Array.Copy(_interfaceHandlers, updated, _interfaceHandlers.Length);
                updated[_interfaceHandlers.Length] = new HandlerEntry<IEventHandler<T>>(handler, priority);
                Array.Sort(updated, (a, b) => b.Priority.CompareTo(a.Priority)); // Higher priority first
                _interfaceHandlers = updated;
            }
        }

        public void Unsubscribe(object handler)
        {
            using (_lock.EnterScope())
            {
                if (handler is Action<T> action)
                {
                    int index = -1;
                    for (int i = 0; i < _actions.Length; i++) if (ReferenceEquals(_actions[i].Delegate, action)) { index = i; break; }
                    if (index != -1)
                    {
                        var updated = new HandlerEntry<Action<T>>[_actions.Length - 1];
                        if (index > 0) Array.Copy(_actions, 0, updated, 0, index);
                        if (index < _actions.Length - 1) Array.Copy(_actions, index + 1, updated, index, _actions.Length - index - 1);
                        _actions = updated;
                    }
                }
                else if (handler is Func<T, ValueTask> asyncAction)
                {
                    int index = -1;
                    for (int i = 0; i < _asyncActions.Length; i++) if (ReferenceEquals(_asyncActions[i].Delegate, asyncAction)) { index = i; break; }
                    if (index != -1)
                    {
                        var updated = new HandlerEntry<Func<T, ValueTask>>[_asyncActions.Length - 1];
                        if (index > 0) Array.Copy(_asyncActions, 0, updated, 0, index);
                        if (index < _asyncActions.Length - 1) Array.Copy(_asyncActions, index + 1, updated, index, _asyncActions.Length - index - 1);
                        _asyncActions = updated;
                    }
                }
                else if (handler is IEventHandler<T> interfaceHandler)
                {
                    int index = -1;
                    for (int i = 0; i < _interfaceHandlers.Length; i++) if (ReferenceEquals(_interfaceHandlers[i].Delegate, interfaceHandler)) { index = i; break; }
                    if (index != -1)
                    {
                        var updated = new HandlerEntry<IEventHandler<T>>[_interfaceHandlers.Length - 1];
                        if (index > 0) Array.Copy(_interfaceHandlers, 0, updated, 0, index);
                        if (index < _interfaceHandlers.Length - 1) Array.Copy(_interfaceHandlers, index + 1, updated, index, _interfaceHandlers.Length - index - 1);
                        _interfaceHandlers = updated;
                    }
                }
            }
        }

        public void Publish(T eventData)
        {
            var interfaces = _interfaceHandlers;
            var actions = _actions;
            var asyncActions = _asyncActions;

            // Zero-handler fast-path
            if (interfaces.Length == 0 && actions.Length == 0 && asyncActions.Length == 0) return;

            for (int i = 0; i < interfaces.Length; i++)
            {
                interfaces[i].Delegate.HandleEvent(eventData);
            }

            for (int i = 0; i < actions.Length; i++)
            {
                actions[i].Delegate(eventData);
            }

            for (int i = 0; i < asyncActions.Length; i++)
            {
                _ = asyncActions[i].Delegate(eventData);
            }
        }

        public void PublishRef(in T eventData)
        {
            var interfaces = _interfaceHandlers;
            var actions = _actions;
            var asyncActions = _asyncActions;

            // Zero-handler fast-path
            if (interfaces.Length == 0 && actions.Length == 0 && asyncActions.Length == 0) return;

            for (int i = 0; i < interfaces.Length; i++)
            {
                interfaces[i].Delegate.HandleEvent(eventData);
            }

            for (int i = 0; i < actions.Length; i++)
            {
                actions[i].Delegate(eventData);
            }

            for (int i = 0; i < asyncActions.Length; i++)
            {
                _ = asyncActions[i].Delegate(eventData);
            }
        }

        public async ValueTask PublishAsync(T eventData)
        {
            var interfaces = _interfaceHandlers;
            var actions = _actions;
            var asyncActions = _asyncActions;

            // Zero-handler fast-path
            if (interfaces.Length == 0 && actions.Length == 0 && asyncActions.Length == 0) return;

            for (int i = 0; i < interfaces.Length; i++)
            {
                interfaces[i].Delegate.HandleEvent(eventData);
            }

            for (int i = 0; i < actions.Length; i++)
            {
                actions[i].Delegate(eventData);
            }

            int asyncCount = asyncActions.Length;
            if (asyncCount == 0) return;

            if (asyncCount == 1)
            {
                var vt = asyncActions[0].Delegate(eventData);
                if (!vt.IsCompleted) await vt;
                else vt.GetAwaiter().GetResult();
                return;
            }

            // Start all async tasks concurrently
            var tasks = ArrayPool<ValueTask>.Shared.Rent(asyncCount);
            try
            {
                for (int i = 0; i < asyncCount; i++)
                {
                    tasks[i] = asyncActions[i].Delegate(eventData);
                }

                for (int i = 0; i < asyncCount; i++)
                {
                    var task = tasks[i];
                    if (!task.IsCompleted) await task;
                    else task.GetAwaiter().GetResult();
                }
            }
            finally
            {
                ArrayPool<ValueTask>.Shared.Return(tasks, clearArray: true);
            }
        }

        void IHandlerList.Publish(object eventData) => Publish((T)eventData);
        void IHandlerList.PublishRef<TEvent>(in TEvent eventData)
        {
            if (typeof(TEvent) == typeof(T))
            {
                PublishRef(in System.Runtime.CompilerServices.Unsafe.As<TEvent, T>(ref System.Runtime.CompilerServices.Unsafe.AsRef(in eventData)));
            }
            else
            {
                Publish((T)(object)eventData);
            }
        }
        ValueTask IHandlerList.PublishAsync(object eventData) => PublishAsync((T)eventData);
    }

    private readonly ConcurrentDictionary<Type, IHandlerList> _typeToHandlers = new();
    private volatile FrozenDictionary<Type, IHandlerList> _frozenHandlers = FrozenDictionary<Type, IHandlerList>.Empty;

    private HandlerList<T> GetHandlers<T>()
    {
        if (_frozenHandlers.TryGetValue(typeof(T), out var handlers)) return (HandlerList<T>)handlers;
        return (HandlerList<T>)_typeToHandlers.GetOrAdd(typeof(T), _ => new HandlerList<T>());
    }

    public void Freeze()
    {
        _frozenHandlers = _typeToHandlers.ToFrozenDictionary();
        _diagnosticBus.Publish("FastEventBus", "Event bus frozen", DiagnosticSeverity.Info, m => {
            m.Add("RegisteredTypes", _frozenHandlers.Count);
        });
    }

    public void Subscribe<T>(Action<T> handler, int priority = 0) => GetHandlers<T>().Subscribe(handler, priority);
    public void SubscribeAsync<T>(Func<T, ValueTask> handler, int priority = 0) => GetHandlers<T>().Subscribe(handler, priority);
    public void Subscribe<T>(IEventHandler<T> handler, int priority = 0) => GetHandlers<T>().Subscribe(handler, priority);

    public void Unsubscribe<T>(Action<T> handler)
    {
        if (_typeToHandlers.TryGetValue(typeof(T), out var handlers))
        {
            handlers.Unsubscribe(handler);
        }
    }

    public void Unsubscribe<T>(IEventHandler<T> handler)
    {
        if (_typeToHandlers.TryGetValue(typeof(T), out var handlers))
        {
            handlers.Unsubscribe(handler);
        }
    }

    public void UnsubscribeAsync<T>(Func<T, ValueTask> handler)
    {
        if (_typeToHandlers.TryGetValue(typeof(T), out var handlers))
        {
            handlers.Unsubscribe(handler);
        }
    }

    public void Publish<T>(T eventData) {
        Interlocked.Increment(ref _totalPublished);
        GetHandlers<T>().Publish(eventData);
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        Clear();
        return Task.CompletedTask;
    }

    public void Publish<T>(in T eventData) where T : struct
    {
        Interlocked.Increment(ref _totalPublished);
        var handlers = GetHandlers<T>();
        handlers.PublishRef(in eventData);
    }

    public ValueTask PublishAsync<T>(T eventData) {
        Interlocked.Increment(ref _totalAsyncPublished);
        return GetHandlers<T>().PublishAsync(eventData);
    }

    public void Clear() => _typeToHandlers.Clear();

    public void Clear<T>() => _typeToHandlers.TryRemove(typeof(T), out _);

    public override Dictionary<string, object> GetDiagnosticInfo()
    {
        var info = base.GetDiagnosticInfo();
        info["TotalPublished"] = Interlocked.Read(ref _totalPublished);
        info["TotalAsyncPublished"] = Interlocked.Read(ref _totalAsyncPublished);
        info["RegisteredEventTypes"] = _typeToHandlers.Count;

        int totalHandlers = 0;
        foreach (var handlerList in _typeToHandlers.Values) {
            totalHandlers += handlerList.Count;
        }
        info["TotalHandlers"] = totalHandlers;

        return info;
    }
}
