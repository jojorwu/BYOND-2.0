using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;

namespace Shared.Services
{
    public class ComponentManager : IComponentManager
    {
        private readonly ConcurrentDictionary<Type, object> _stores = new();

        public event EventHandler<ComponentEventArgs>? ComponentAdded;
        public event EventHandler<ComponentEventArgs>? ComponentRemoved;

        private Dictionary<int, T> GetStore<T>() where T : class, IComponent
        {
            return (Dictionary<int, T>)_stores.GetOrAdd(typeof(T), _ => new Dictionary<int, T>());
        }

        public void AddComponent<T>(IGameObject owner, T component) where T : class, IComponent
        {
            var store = GetStore<T>();
            lock (store)
            {
                component.Owner = owner;
                store[owner.Id] = component;
                component.Initialize();
            }
            ComponentAdded?.Invoke(this, new ComponentEventArgs(owner, component, typeof(T)));
        }

        public void RemoveComponent<T>(IGameObject owner) where T : class, IComponent
        {
            var store = GetStore<T>();
            IComponent? removedComponent = null;
            lock (store)
            {
                if (store.Remove(owner.Id, out var component))
                {
                    removedComponent = component;
                    component.Shutdown();
                    component.Owner = null;
                }
            }
            if (removedComponent != null)
            {
                ComponentRemoved?.Invoke(this, new ComponentEventArgs(owner, removedComponent, typeof(T)));
            }
        }

        public T? GetComponent<T>(IGameObject owner) where T : class, IComponent
        {
            var store = GetStore<T>();
            lock (store)
            {
                store.TryGetValue(owner.Id, out var component);
                return component;
            }
        }

        public IEnumerable<T> GetComponents<T>() where T : class, IComponent
        {
            var store = GetStore<T>();
            lock (store)
            {
                return store.Values.ToList();
            }
        }

        public IEnumerable<IComponent> GetAllComponents(IGameObject owner)
        {
            var components = new List<IComponent>();
            foreach (var storeObj in _stores.Values)
            {
                // This is a bit slow, but GetAllComponents for a single object is rare in DOD
                if (storeObj is System.Collections.IDictionary dict && dict.Contains(owner.Id))
                {
                    components.Add((IComponent)dict[owner.Id]!);
                }
            }
            return components;
        }
    }
}
