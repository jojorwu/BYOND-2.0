using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Messaging;

namespace Shared.Services;

public class FastEventBus : EngineService, IEventBus, IFreezable
{
    private interface IHandlerList
    {
        void Publish(object eventData);
        void PublishRef<TEvent>(in TEvent eventData) where TEvent : struct;
        ValueTask PublishAsync(object eventData);
        void Unsubscribe(object handler);
    }

    private class HandlerList<T> : IHandlerList
    {
        private volatile Action<T>[] _actions = Array.Empty<Action<T>>();
        private volatile Func<T, ValueTask>[] _asyncActions = Array.Empty<Func<T, ValueTask>>();
        private volatile IEventHandler<T>[] _interfaceHandlers = Array.Empty<IEventHandler<T>>();
        private readonly System.Threading.Lock _lock = new();

        public void Subscribe(Action<T> handler)
        {
            using (_lock.EnterScope())
            {
                var updated = new Action<T>[_actions.Length + 1];
                Array.Copy(_actions, updated, _actions.Length);
                updated[_actions.Length] = handler;
                _actions = updated;
            }
        }

        public void Subscribe(Func<T, ValueTask> handler)
        {
            using (_lock.EnterScope())
            {
                var updated = new Func<T, ValueTask>[_asyncActions.Length + 1];
                Array.Copy(_asyncActions, updated, _asyncActions.Length);
                updated[_asyncActions.Length] = handler;
                _asyncActions = updated;
            }
        }

        public void Subscribe(IEventHandler<T> handler)
        {
            using (_lock.EnterScope())
            {
                var updated = new IEventHandler<T>[_interfaceHandlers.Length + 1];
                Array.Copy(_interfaceHandlers, updated, _interfaceHandlers.Length);
                updated[_interfaceHandlers.Length] = handler;
                _interfaceHandlers = updated;
            }
        }

        public void Unsubscribe(object handler)
        {
            using (_lock.EnterScope())
            {
                if (handler is Action<T> action)
                {
                    var index = Array.IndexOf(_actions, action);
                    if (index != -1)
                    {
                        var updated = new Action<T>[_actions.Length - 1];
                        if (index > 0) Array.Copy(_actions, 0, updated, 0, index);
                        if (index < _actions.Length - 1) Array.Copy(_actions, index + 1, updated, index, _actions.Length - index - 1);
                        _actions = updated;
                    }
                }
                else if (handler is Func<T, ValueTask> asyncAction)
                {
                    var index = Array.IndexOf(_asyncActions, asyncAction);
                    if (index != -1)
                    {
                        var updated = new Func<T, ValueTask>[_asyncActions.Length - 1];
                        if (index > 0) Array.Copy(_asyncActions, 0, updated, 0, index);
                        if (index < _asyncActions.Length - 1) Array.Copy(_asyncActions, index + 1, updated, index, _asyncActions.Length - index - 1);
                        _asyncActions = updated;
                    }
                }
                else if (handler is IEventHandler<T> interfaceHandler)
                {
                    var index = Array.IndexOf(_interfaceHandlers, interfaceHandler);
                    if (index != -1)
                    {
                        var updated = new IEventHandler<T>[_interfaceHandlers.Length - 1];
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
            for (int i = 0; i < interfaces.Length; i++)
            {
                interfaces[i].HandleEvent(eventData);
            }

            var actions = _actions;
            for (int i = 0; i < actions.Length; i++)
            {
                actions[i](eventData);
            }

            var asyncActions = _asyncActions;
            for (int i = 0; i < asyncActions.Length; i++)
            {
                _ = asyncActions[i](eventData);
            }
        }

        public void PublishRef(in T eventData)
        {
            var interfaces = _interfaceHandlers;
            for (int i = 0; i < interfaces.Length; i++)
            {
                interfaces[i].HandleEvent(eventData);
            }

            var actions = _actions;
            for (int i = 0; i < actions.Length; i++)
            {
                actions[i](eventData);
            }

            var asyncActions = _asyncActions;
            for (int i = 0; i < asyncActions.Length; i++)
            {
                _ = asyncActions[i](eventData);
            }
        }

        public async ValueTask PublishAsync(T eventData)
        {
            var interfaces = _interfaceHandlers;
            for (int i = 0; i < interfaces.Length; i++)
            {
                interfaces[i].HandleEvent(eventData);
            }

            var actions = _actions;
            for (int i = 0; i < actions.Length; i++)
            {
                actions[i](eventData);
            }

            var asyncActions = _asyncActions;
            int asyncCount = asyncActions.Length;
            if (asyncCount == 0) return;

            if (asyncCount == 1)
            {
                var task = asyncActions[0](eventData);
                if (!task.IsCompleted) await task;
                else task.GetAwaiter().GetResult();
                return;
            }

            // Start all async tasks concurrently
            var tasks = new ValueTask[asyncCount];
            for (int i = 0; i < asyncCount; i++)
            {
                tasks[i] = asyncActions[i](eventData);
            }

            for (int i = 0; i < asyncCount; i++)
            {
                var task = tasks[i];
                if (!task.IsCompleted) await task;
                else task.GetAwaiter().GetResult();
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

    public void Freeze()
    {
        _frozenHandlers = _typeToHandlers.ToFrozenDictionary();
    }

    private HandlerList<T> GetHandlers<T>()
    {
        if (_frozenHandlers.TryGetValue(typeof(T), out var handlers))
        {
            return (HandlerList<T>)handlers;
        }
        return (HandlerList<T>)_typeToHandlers.GetOrAdd(typeof(T), _ => new HandlerList<T>());
    }

    public void Subscribe<T>(Action<T> handler) => GetHandlers<T>().Subscribe(handler);
    public void SubscribeAsync<T>(Func<T, ValueTask> handler) => GetHandlers<T>().Subscribe(handler);
    public void Subscribe<T>(IEventHandler<T> handler) => GetHandlers<T>().Subscribe(handler);

    public void Unsubscribe<T>(Action<T> handler)
    {
        if (_frozenHandlers.TryGetValue(typeof(T), out var handlers))
        {
            handlers.Unsubscribe(handler);
        }
        else if (_typeToHandlers.TryGetValue(typeof(T), out handlers))
        {
            handlers.Unsubscribe(handler);
        }
    }

    public void Unsubscribe<T>(IEventHandler<T> handler)
    {
        if (_frozenHandlers.TryGetValue(typeof(T), out var handlers))
        {
            handlers.Unsubscribe(handler);
        }
        else if (_typeToHandlers.TryGetValue(typeof(T), out handlers))
        {
            handlers.Unsubscribe(handler);
        }
    }

    public void UnsubscribeAsync<T>(Func<T, ValueTask> handler)
    {
        if (_frozenHandlers.TryGetValue(typeof(T), out var handlers))
        {
            handlers.Unsubscribe(handler);
        }
        else if (_typeToHandlers.TryGetValue(typeof(T), out handlers))
        {
            handlers.Unsubscribe(handler);
        }
    }

    public void Publish<T>(T eventData) => GetHandlers<T>().Publish(eventData);

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        Clear();
        return base.StopAsync(cancellationToken);
    }

    public void Publish<T>(in T eventData) where T : struct
    {
        var handlers = GetHandlers<T>();
        handlers.PublishRef(in eventData);
    }

    public ValueTask PublishAsync<T>(T eventData) => GetHandlers<T>().PublishAsync(eventData);

    public void Clear()
    {
        _typeToHandlers.Clear();
        _frozenHandlers = FrozenDictionary<Type, IHandlerList>.Empty;
    }

    public void Clear<T>()
    {
        _typeToHandlers.TryRemove(typeof(T), out _);
        _frozenHandlers = _typeToHandlers.ToFrozenDictionary();
    }
}
