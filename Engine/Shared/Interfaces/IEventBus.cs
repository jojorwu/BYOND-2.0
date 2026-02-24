using System;
using System.Threading.Tasks;

namespace Shared.Messaging;
    /// <summary>
    /// A simple, thread-safe event bus for decoupled communication.
    /// </summary>
    public interface IEventBus
    {
        void Subscribe<T>(Action<T> handler);
        void SubscribeAsync<T>(Func<T, Task> handler);
        void Unsubscribe<T>(Action<T> handler);
        void UnsubscribeAsync<T>(Func<T, Task> handler);
        void Publish<T>(T eventData);
        Task PublishAsync<T>(T eventData);

        /// <summary>
        /// Removes all subscriptions from the event bus.
        /// </summary>
        void Clear();

        /// <summary>
        /// Removes all subscriptions for a specific event type.
        /// </summary>
        void Clear<T>();
    }
