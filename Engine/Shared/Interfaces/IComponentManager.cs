using System;
using System.Collections.Generic;

namespace Shared.Interfaces
{
    public class ComponentEventArgs : EventArgs
    {
        public IGameObject Owner { get; }
        public IComponent Component { get; }
        public Type ComponentType { get; }

        public ComponentEventArgs(IGameObject owner, IComponent component, Type componentType)
        {
            Owner = owner;
            Component = component;
            ComponentType = componentType;
        }
    }

    public interface IComponentManager
    {
        event EventHandler<ComponentEventArgs>? ComponentAdded;
        event EventHandler<ComponentEventArgs>? ComponentRemoved;

        void AddComponent<T>(IGameObject owner, T component) where T : class, IComponent;
        void RemoveComponent<T>(IGameObject owner) where T : class, IComponent;
        T? GetComponent<T>(IGameObject owner) where T : class, IComponent;
        IEnumerable<T> GetComponents<T>() where T : class, IComponent;
        IEnumerable<IComponent> GetAllComponents(IGameObject owner);

        /// <summary>
        /// Compacts internal storage to reclaim memory.
        /// </summary>
        void Compact();
    }
}
