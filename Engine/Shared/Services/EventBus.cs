using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Shared.Messaging;

namespace Shared.Services
{
    public class EventBus : IEventBus
    {
        private readonly ConcurrentDictionary<Type, ImmutableList<Delegate>> _handlers = new();

        public void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            _handlers.AddOrUpdate(type,
                ImmutableList.Create<Delegate>(handler),
                (t, list) => list.Add(handler));
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var list))
            {
                var newList = list.Remove(handler);
                if (newList.IsEmpty)
                {
                    _handlers.TryRemove(type, out _);
                }
                else
                {
                    _handlers.TryUpdate(type, newList, list);
                }
            }
        }

        public void Publish<T>(T eventData)
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var list))
            {
                foreach (var handler in list)
                {
                    ((Action<T>)handler)(eventData);
                }
            }
        }
    }
}
