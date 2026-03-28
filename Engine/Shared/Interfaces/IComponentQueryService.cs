using System;
using System.Collections.Generic;

namespace Shared.Interfaces;
    public interface IComponentQueryService
    {
        IEnumerable<IGameObject> Query<T>() where T : class, IComponent;
        IEnumerable<IGameObject> Query(params ReadOnlySpan<Type> componentTypes);
        IEntityQuery GetQuery(params ReadOnlySpan<Type> componentTypes);

        void Subscribe<T>(Action<ComponentEventArgs> onAdded, Action<ComponentEventArgs> onRemoved) where T : class, IComponent;
        void Unsubscribe<T>(Action<ComponentEventArgs> onAdded, Action<ComponentEventArgs> onRemoved) where T : class, IComponent;

        /// <summary>
        /// Clears all cached query results.
        /// </summary>
        void Clear();
    }
