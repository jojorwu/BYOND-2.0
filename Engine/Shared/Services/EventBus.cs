using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Shared.Messaging;

namespace Shared.Services;
    public class EventBus : IEventBus
    {
        private readonly ConcurrentDictionary<Type, object[]> _handlers = new();
        private readonly System.Threading.Lock _lock = new();

        public void Subscribe<T>(Action<T> handler, int priority = 0)
        {
            SubscribeInternal(typeof(T), handler);
        }

        public void SubscribeAsync<T>(Func<T, ValueTask> handler, int priority = 0)
        {
            SubscribeInternal(typeof(T), handler);
        }

        public void Subscribe<T>(IEventHandler<T> handler, int priority = 0)
        {
            SubscribeInternal(typeof(T), handler);
        }

        private void SubscribeInternal(Type type, object handler)
        {
            using (_lock.EnterScope())
            {
                _handlers.AddOrUpdate(type,
                    _ => new[] { handler },
                    (_, existing) =>
                    {
                        var updated = new object[existing.Length + 1];
                        Array.Copy(existing, updated, existing.Length);
                        updated[existing.Length] = handler;
                        return updated;
                    });
            }
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            UnsubscribeInternal(typeof(T), handler);
        }

        public void UnsubscribeAsync<T>(Func<T, ValueTask> handler)
        {
            UnsubscribeInternal(typeof(T), handler);
        }

        public void Unsubscribe<T>(IEventHandler<T> handler)
        {
            UnsubscribeInternal(typeof(T), handler);
        }

        private void UnsubscribeInternal(Type type, object handler)
        {
            using (_lock.EnterScope())
            {
                if (_handlers.TryGetValue(type, out var existing))
                {
                    var index = Array.IndexOf(existing, handler);
                    if (index == -1) return;

                    if (existing.Length == 1)
                    {
                        _handlers.TryRemove(type, out _);
                    }
                    else
                    {
                        var updated = new object[existing.Length - 1];
                        Array.Copy(existing, 0, updated, 0, index);
                        Array.Copy(existing, index + 1, updated, index, existing.Length - index - 1);
                        _handlers[type] = updated;
                    }
                }
            }
        }

        public void Publish<T>(in T eventData) where T : struct
        {
            Publish(eventData);
        }

        public void Publish<T>(T eventData)
        {
            if (_handlers.TryGetValue(typeof(T), out var handlers))
            {
                // Snapshot-style read: the array itself is never modified in-place
                var span = handlers.AsSpan();
                for (int i = 0; i < span.Length; i++)
                {
                    var handler = span[i];
                    if (handler is IEventHandler<T> interfaceHandler)
                    {
                        interfaceHandler.HandleEvent(eventData);
                    }
                    else if (handler is Action<T> action)
                    {
                        action(eventData);
                    }
                    else if (handler is Func<T, ValueTask> asyncAction)
                    {
                        _ = asyncAction(eventData);
                    }
                }
            }
        }

        public async ValueTask PublishAsync<T>(T eventData)
        {
            if (_handlers.TryGetValue(typeof(T), out var handlers))
            {
                var span = handlers.AsSpan();

                // Fast path for single handler
                if (span.Length == 1)
                {
                    var handler = span[0];
                    if (handler is IEventHandler<T> interfaceHandler)
                    {
                        interfaceHandler.HandleEvent(eventData);
                    }
                    else if (handler is Action<T> action)
                    {
                        action(eventData);
                    }
                    else if (handler is Func<T, ValueTask> asyncAction)
                    {
                        await asyncAction(eventData);
                    }
                    return;
                }

                // Collect tasks for multiple handlers
                List<ValueTask>? tasks = null;
                for (int i = 0; i < span.Length; i++)
                {
                    var handler = span[i];
                    if (handler is IEventHandler<T> interfaceHandler)
                    {
                        interfaceHandler.HandleEvent(eventData);
                    }
                    else if (handler is Action<T> action)
                    {
                        action(eventData);
                    }
                    else if (handler is Func<T, ValueTask> asyncAction)
                    {
                        tasks ??= new List<ValueTask>(span.Length);
                        tasks.Add(asyncAction(eventData));
                    }
                }

                if (tasks != null)
                {
                    foreach (var task in tasks) await task;
                }
            }
        }

        public void Clear()
        {
            using (_lock.EnterScope())
            {
                _handlers.Clear();
            }
        }

        public void Clear<T>()
        {
            using (_lock.EnterScope())
            {
                _handlers.TryRemove(typeof(T), out _);
            }
        }
    }
