using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
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

        public void SubscribeAsync<T>(Func<T, Task> handler)
        {
            var type = typeof(T);
            _handlers.AddOrUpdate(type,
                ImmutableList.Create<Delegate>(handler),
                (t, list) => list.Add(handler));
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            UnsubscribeInternal(typeof(T), handler);
        }

        public void UnsubscribeAsync<T>(Func<T, Task> handler)
        {
            UnsubscribeInternal(typeof(T), handler);
        }

        private void UnsubscribeInternal(Type type, Delegate handler)
        {
            while (_handlers.TryGetValue(type, out var list))
            {
                var newList = list.Remove(handler);
                if (newList.IsEmpty)
                {
                    if (((IDictionary<Type, ImmutableList<Delegate>>)_handlers).Remove(new KeyValuePair<Type, ImmutableList<Delegate>>(type, list)))
                        break;
                }
                else
                {
                    if (_handlers.TryUpdate(type, newList, list))
                        break;
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
                    if (handler is Action<T> action)
                    {
                        action(eventData);
                    }
                    else if (handler is Func<T, Task> asyncAction)
                    {
                        // Fire and forget for sync Publish
                        _ = asyncAction(eventData);
                    }
                }
            }
        }

        public async Task PublishAsync<T>(T eventData)
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var list))
            {
                var tasks = new List<Task>();
                foreach (var handler in list)
                {
                    if (handler is Action<T> action)
                    {
                        action(eventData);
                    }
                    else if (handler is Func<T, Task> asyncAction)
                    {
                        tasks.Add(asyncAction(eventData));
                    }
                }

                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks);
                }
            }
        }
    }
}
