using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shared.Messaging;

namespace Shared.Services
{
    public class EventBus : IEventBus
    {
        private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
        private readonly object _lock = new();

        public void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            lock (_lock)
            {
                var handlers = _handlers.GetOrAdd(type, _ => new List<Delegate>());
                handlers.Add(handler);
            }
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            lock (_lock)
            {
                if (_handlers.TryGetValue(type, out var handlers))
                {
                    handlers.Remove(handler);
                }
            }
        }

        public void Publish<T>(T eventData)
        {
            var type = typeof(T);
            Delegate[]? handlersCopy = null;

            lock (_lock)
            {
                if (_handlers.TryGetValue(type, out var handlers))
                {
                    handlersCopy = handlers.ToArray();
                }
            }

            if (handlersCopy != null)
            {
                foreach (var handler in handlersCopy)
                {
                    ((Action<T>)handler)(eventData);
                }
            }
        }
    }
}
