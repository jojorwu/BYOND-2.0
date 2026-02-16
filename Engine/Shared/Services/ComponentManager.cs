using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;

namespace Shared.Services
{
    public class ComponentManager : IComponentManager
    {
        private readonly IArchetypeManager _archetypeManager;

        public event EventHandler<ComponentEventArgs>? ComponentAdded;
        public event EventHandler<ComponentEventArgs>? ComponentRemoved;

        public ComponentManager(IArchetypeManager archetypeManager)
        {
            _archetypeManager = archetypeManager;
        }

        public void AddComponent<T>(IGameObject owner, T component) where T : class, IComponent
        {
            AddComponent(owner, (IComponent)component);
        }

        public void AddComponent(IGameObject owner, IComponent component)
        {
            _archetypeManager.AddComponent(owner, component);
            ComponentAdded?.Invoke(this, new ComponentEventArgs(owner, component, component.GetType()));
        }

        public void RemoveComponent<T>(IGameObject owner) where T : class, IComponent
        {
            RemoveComponent(owner, typeof(T));
        }

        public void RemoveComponent(IGameObject owner, Type componentType)
        {
            var component = _archetypeManager.GetAllComponents(owner.Id).FirstOrDefault(c => c.GetType() == componentType);
            if (component != null)
            {
                _archetypeManager.RemoveComponent(owner, componentType);
                ComponentRemoved?.Invoke(this, new ComponentEventArgs(owner, component, componentType));
            }
        }

        public T? GetComponent<T>(IGameObject owner) where T : class, IComponent
        {
            return _archetypeManager.GetComponent<T>(owner.Id);
        }

        public IComponent? GetComponent(IGameObject owner, Type componentType)
        {
            return _archetypeManager.GetComponent(owner.Id, componentType);
        }

        public IEnumerable<T> GetComponents<T>() where T : class, IComponent
        {
            return _archetypeManager.GetComponents<T>();
        }

        public IEnumerable<IComponent> GetComponents(Type componentType)
        {
            return _archetypeManager.GetComponents(componentType);
        }

        public IEnumerable<IComponent> GetAllComponents(IGameObject owner)
        {
            return _archetypeManager.GetAllComponents(owner.Id);
        }

        public void Compact()
        {
            _archetypeManager.Compact();
        }

        public void Shrink() => Compact();
    }
}
