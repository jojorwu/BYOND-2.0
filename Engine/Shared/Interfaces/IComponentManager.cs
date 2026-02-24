using System;
using System.Collections.Generic;

namespace Shared.Interfaces;
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

    public interface IComponentManager : IShrinkable
    {
        event EventHandler<ComponentEventArgs>? ComponentAdded;
        event EventHandler<ComponentEventArgs>? ComponentRemoved;

        void AddComponent<T>(IGameObject owner, T component) where T : class, IComponent;
        void AddComponent(IGameObject owner, IComponent component);
        void RemoveComponent<T>(IGameObject owner) where T : class, IComponent;
        void RemoveComponent(IGameObject owner, Type componentType);
        T? GetComponent<T>(IGameObject owner) where T : class, IComponent;
        IComponent? GetComponent(IGameObject owner, Type componentType);
        IEnumerable<T> GetComponents<T>() where T : class, IComponent;
        IEnumerable<Models.ArchetypeChunk<T>> GetChunks<T>() where T : class, IComponent;
        IEnumerable<IComponent> GetComponents(Type componentType);
        IEnumerable<IComponent> GetAllComponents(IGameObject owner);

        /// <summary>
        /// Compacts internal storage to reclaim memory.
        /// </summary>
        void Compact();
    }
