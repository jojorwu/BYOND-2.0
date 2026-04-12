using Shared;
using Shared.Attributes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Core.Api;

[EngineService(typeof(IEventApi))]
public class EventApi : IEventApi
{
    public string Name => "Events";
    private readonly ConcurrentDictionary<string, List<Action<object[]>>> _subscribers = new();

    public void Publish(string eventName, params object[] args)
    {
        if (_subscribers.TryGetValue(eventName, out var callbacks))
        {
            Action<object[]>[] callbacksCopy;
            lock (callbacks)
            {
                callbacksCopy = callbacks.ToArray();
            }

            foreach (var callback in callbacksCopy)
            {
                try
                {
                    callback(args);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] EventApi.Publish failed for event '{eventName}': {ex.Message}");
                }
            }
        }
    }

    public void Subscribe(string eventName, Action<object[]> callback)
    {
        var callbacks = _subscribers.GetOrAdd(eventName, _ => new List<Action<object[]>>());
        lock (callbacks)
        {
            callbacks.Add(callback);
        }
    }

    public void Unsubscribe(string eventName, Action<object[]> callback)
    {
        if (_subscribers.TryGetValue(eventName, out var callbacks))
        {
            lock (callbacks)
            {
                callbacks.Remove(callback);
            }
        }
    }
}
