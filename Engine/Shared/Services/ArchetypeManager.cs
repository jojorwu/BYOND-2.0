using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services
{
    public interface IArchetypeManager
    {
        void AddEntity(IGameObject entity);
        void RemoveEntity(int entityId);
        void AddComponent<T>(IGameObject entity, T component) where T : class, IComponent;
        void RemoveComponent<T>(IGameObject entity) where T : class, IComponent;
        T? GetComponent<T>(int entityId) where T : class, IComponent;
        IEnumerable<T> GetComponents<T>() where T : class, IComponent;
        IEnumerable<IComponent> GetAllComponents(int entityId);
        void Compact();
    }

    public class ArchetypeManager : IArchetypeManager
    {
        private readonly List<Archetype> _archetypes = new();
        private readonly ConcurrentDictionary<int, Archetype> _entityToArchetype = new();
        private readonly ConcurrentDictionary<int, Dictionary<Type, IComponent>> _entityComponents = new();

        public void AddEntity(IGameObject entity)
        {
            _entityComponents[entity.Id] = new Dictionary<Type, IComponent>();
            MoveToArchetype(entity.Id);
        }

        public void RemoveEntity(int entityId)
        {
            if (_entityToArchetype.TryRemove(entityId, out var archetype))
            {
                archetype.RemoveEntity(entityId);
            }
            _entityComponents.TryRemove(entityId, out _);
        }

        public void AddComponent<T>(IGameObject entity, T component) where T : class, IComponent
        {
            var components = _entityComponents.GetOrAdd(entity.Id, _ => new Dictionary<Type, IComponent>());
            components[typeof(T)] = component;
            component.Owner = entity;
            component.Initialize();
            MoveToArchetype(entity.Id);
        }

        public void RemoveComponent<T>(IGameObject entity) where T : class, IComponent
        {
            if (_entityComponents.TryGetValue(entity.Id, out var components))
            {
                if (components.Remove(typeof(T), out var component))
                {
                    component.Shutdown();
                    component.Owner = null;
                    MoveToArchetype(entity.Id);
                }
            }
        }

        private void MoveToArchetype(int entityId)
        {
            var components = _entityComponents[entityId];
            var signature = components.Keys.OrderBy(t => t.FullName).ToList();

            // Find or create archetype
            Archetype? targetArchetype;
            lock (_archetypes)
            {
                targetArchetype = _archetypes.FirstOrDefault(a => a.Signature.SetEquals(signature));
                if (targetArchetype == null)
                {
                    targetArchetype = new Archetype(signature);
                    _archetypes.Add(targetArchetype);
                }
            }

            // Remove from old
            if (_entityToArchetype.TryGetValue(entityId, out var oldArchetype))
            {
                if (oldArchetype == targetArchetype) return;
                oldArchetype.RemoveEntity(entityId);
            }

            // Add to new
            targetArchetype.AddEntity(entityId, components);
            _entityToArchetype[entityId] = targetArchetype;
        }

        public T? GetComponent<T>(int entityId) where T : class, IComponent
        {
            if (_entityToArchetype.TryGetValue(entityId, out var archetype))
            {
                return archetype.GetComponent(entityId, typeof(T)) as T;
            }
            return null;
        }

        public IEnumerable<T> GetComponents<T>() where T : class, IComponent
        {
            var results = new List<T>();
            lock (_archetypes)
            {
                foreach (var archetype in _archetypes)
                {
                    if (archetype.Signature.Contains(typeof(T)))
                    {
                        results.AddRange(archetype.GetComponents<T>());
                    }
                }
            }
            return results;
        }

        public IEnumerable<IComponent> GetAllComponents(int entityId)
        {
            if (_entityComponents.TryGetValue(entityId, out var components))
            {
                return components.Values;
            }
            return Enumerable.Empty<IComponent>();
        }

        public void Compact()
        {
            lock (_archetypes)
            {
                foreach (var archetype in _archetypes)
                {
                    archetype.Compact();
                }
            }
        }
    }
}
