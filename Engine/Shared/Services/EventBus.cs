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
        private readonly ConcurrentDictionary<Type, Delegate[]> _handlers = new();
        private readonly object _lock = new();

        public void Subscribe<T>(Action<T> handler)
        {
            SubscribeInternal(typeof(T), handler);
        }

        public void SubscribeAsync<T>(Func<T, Task> handler)
        {
            SubscribeInternal(typeof(T), handler);
        }

        private void SubscribeInternal(Type type, Delegate handler)
        {
            lock (_lock)
            {
                _handlers.AddOrUpdate(type,
                    _ => new[] { handler },
                    (_, existing) =>
                    {
                        var updated = new Delegate[existing.Length + 1];
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

        public void UnsubscribeAsync<T>(Func<T, Task> handler)
        {
            UnsubscribeInternal(typeof(T), handler);
        }

        private void UnsubscribeInternal(Type type, Delegate handler)
        {
            lock (_lock)
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
                        var updated = new Delegate[existing.Length - 1];
                        Array.Copy(existing, 0, updated, 0, index);
                        Array.Copy(existing, index + 1, updated, index, existing.Length - index - 1);
                        _handlers[type] = updated;
                    }
                }
            }
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
                    if (handler is Action<T> action)
                    {
                        action(eventData);
                    }
                    else if (handler is Func<T, Task> asyncAction)
                    {
                        _ = asyncAction(eventData);
                    }
                }
            }
        }

        public async Task PublishAsync<T>(T eventData)
        {
            if (_handlers.TryGetValue(typeof(T), out var handlers))
            {
                var span = handlers.AsSpan();

                // Fast path for single handler
                if (span.Length == 1)
                {
                    var handler = span[0];
                    if (handler is Action<T> action)
                    {
                        action(eventData);
                    }
                    else if (handler is Func<T, Task> asyncAction)
                    {
                        await asyncAction(eventData);
                    }
                    return;
                }

                // Collect tasks for multiple handlers
                List<Task>? tasks = null;
                for (int i = 0; i < span.Length; i++)
                {
                    var handler = span[i];
                    if (handler is Action<T> action)
                    {
                        action(eventData);
                    }
                    else if (handler is Func<T, Task> asyncAction)
                    {
                        tasks ??= new List<Task>(span.Length);
                        tasks.Add(asyncAction(eventData));
                    }
                }

                if (tasks != null)
                {
                    await Task.WhenAll(tasks);
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _handlers.Clear();
            }
        }

        public void Clear<T>()
        {
            lock (_lock)
            {
                _handlers.TryRemove(typeof(T), out _);
            }
        }
    }
