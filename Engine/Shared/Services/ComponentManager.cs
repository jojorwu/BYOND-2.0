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
            _archetypeManager.AddComponent(owner, component);
            ComponentAdded?.Invoke(this, new ComponentEventArgs(owner, component, typeof(T)));
        }

        public void RemoveComponent<T>(IGameObject owner) where T : class, IComponent
        {
            var component = _archetypeManager.GetComponent<T>(owner.Id);
            if (component != null)
            {
                _archetypeManager.RemoveComponent<T>(owner);
                ComponentRemoved?.Invoke(this, new ComponentEventArgs(owner, component, typeof(T)));
            }
        }

        public T? GetComponent<T>(IGameObject owner) where T : class, IComponent
        {
            return _archetypeManager.GetComponent<T>(owner.Id);
        }

        public IEnumerable<T> GetComponents<T>() where T : class, IComponent
        {
            return _archetypeManager.GetComponents<T>();
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
