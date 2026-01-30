using System;

namespace Shared.Messaging
{
    /// <summary>
    /// A simple, thread-safe event bus for decoupled communication.
    /// </summary>
    public interface IEventBus
    {
        void Subscribe<T>(Action<T> handler);
        void Unsubscribe<T>(Action<T> handler);
        void Publish<T>(T eventData);
    }
}
