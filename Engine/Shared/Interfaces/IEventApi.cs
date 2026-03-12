using System;

using Shared.Interfaces;

namespace Shared;

public interface IEventApi : IApiProvider
{
    /// <summary>
    /// Publishes a custom event to all subscribers.
    /// </summary>
    void Publish(string eventName, params object[] args);

    /// <summary>
    /// Subscribes a callback to the specified event.
    /// </summary>
    void Subscribe(string eventName, Action<object[]> callback);

    /// <summary>
    /// Unsubscribes a callback from the specified event.
    /// </summary>
    void Unsubscribe(string eventName, Action<object[]> callback);
}
