using System;
using System.Threading.Tasks;

namespace Shared.Messaging;

    public interface IEventHandler<T>
    {
        void HandleEvent(T eventData);
    }

    /// <summary>
    /// A simple, thread-safe event bus for decoupled communication.
    /// Optimized for low-allocation using ValueTask.
    /// </summary>
    public interface IEventBus
    {
        void Subscribe<T>(Action<T> handler);
        void SubscribeAsync<T>(Func<T, ValueTask> handler);
        void Subscribe<T>(IEventHandler<T> handler);
        void Unsubscribe<T>(Action<T> handler);
        void UnsubscribeAsync<T>(Func<T, ValueTask> handler);
        void Unsubscribe<T>(IEventHandler<T> handler);
        void Publish<T>(T eventData);
        void Publish<T>(in T eventData) where T : struct;
        ValueTask PublishAsync<T>(T eventData);

        /// <summary>
        /// Removes all subscriptions from the event bus.
        /// </summary>
        void Clear();

        /// <summary>
        /// Removes all subscriptions for a specific event type.
        /// </summary>
        void Clear<T>();
    }
