using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared.Messaging;

namespace Shared.Services;

public class FastEventBus : IEventBus
{
    private interface IHandlerList
    {
        void Publish(object eventData);
        ValueTask PublishAsync(object eventData);
        void Unsubscribe(Delegate handler);
    }

    private class HandlerList<T> : IHandlerList
    {
        private volatile Action<T>[] _actions = Array.Empty<Action<T>>();
        private volatile Func<T, ValueTask>[] _asyncActions = Array.Empty<Func<T, ValueTask>>();
        private readonly object _lock = new();

        public void Subscribe(Action<T> handler)
        {
            lock (_lock)
            {
                var updated = new Action<T>[_actions.Length + 1];
                Array.Copy(_actions, updated, _actions.Length);
                updated[_actions.Length] = handler;
                _actions = updated;
            }
        }

        public void Subscribe(Func<T, ValueTask> handler)
        {
            lock (_lock)
            {
                var updated = new Func<T, ValueTask>[_asyncActions.Length + 1];
                Array.Copy(_asyncActions, updated, _asyncActions.Length);
                updated[_asyncActions.Length] = handler;
                _asyncActions = updated;
            }
        }

        public void Unsubscribe(Delegate handler)
        {
            lock (_lock)
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
            }
        }

        public void Publish(T eventData)
        {
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
            var actions = _actions;
            for (int i = 0; i < actions.Length; i++)
            {
                actions[i](eventData);
            }

            var asyncActions = _asyncActions;
            for (int i = 0; i < asyncActions.Length; i++)
            {
                await asyncActions[i](eventData);
            }
        }

        void IHandlerList.Publish(object eventData) => Publish((T)eventData);
        ValueTask IHandlerList.PublishAsync(object eventData) => PublishAsync((T)eventData);
    }

    private readonly ConcurrentDictionary<Type, IHandlerList> _typeToHandlers = new();

    private HandlerList<T> GetHandlers<T>()
    {
        return (HandlerList<T>)_typeToHandlers.GetOrAdd(typeof(T), _ => new HandlerList<T>());
    }

    public void Subscribe<T>(Action<T> handler) => GetHandlers<T>().Subscribe(handler);
    public void SubscribeAsync<T>(Func<T, ValueTask> handler) => GetHandlers<T>().Subscribe(handler);

    public void Unsubscribe<T>(Action<T> handler)
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

    public void Publish<T>(T eventData) => GetHandlers<T>().Publish(eventData);

    public ValueTask PublishAsync<T>(T eventData) => GetHandlers<T>().PublishAsync(eventData);

    public void Clear() => _typeToHandlers.Clear();

    public void Clear<T>() => _typeToHandlers.TryRemove(typeof(T), out _);
}
