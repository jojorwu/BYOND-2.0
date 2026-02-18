using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services
{
    public interface IArchetypeManager
    {
        void AddEntity(IGameObject entity);
        void RemoveEntity(int entityId);
        void AddComponent<T>(IGameObject entity, T component) where T : class, IComponent;
        void AddComponent(IGameObject entity, IComponent component);
        void RemoveComponent<T>(IGameObject entity) where T : class, IComponent;
        void RemoveComponent(IGameObject entity, Type componentType);
        T? GetComponent<T>(int entityId) where T : class, IComponent;
        IComponent? GetComponent(int entityId, Type componentType);
        IEnumerable<T> GetComponents<T>() where T : class, IComponent;
        IEnumerable<ArchetypeChunk<T>> GetChunks<T>() where T : class, IComponent;
        IEnumerable<IComponent> GetComponents(Type componentType);
        IEnumerable<IComponent> GetAllComponents(int entityId);
        IEnumerable<Archetype> GetArchetypesWithComponents(params Type[] componentTypes);
        void Compact();
    }

    public class ArchetypeManager : IArchetypeManager
    {
        private readonly List<Archetype> _archetypes = new();
        private readonly Dictionary<string, Archetype> _signatureToArchetype = new();
        private readonly ConcurrentDictionary<Type, Archetype[]> _typeToArchetypesCache = new();
        private readonly ConcurrentDictionary<int, Archetype> _entityToArchetype = new();
        private readonly ConcurrentDictionary<int, Dictionary<Type, IComponent>> _entityComponents = new();
        private readonly ConcurrentDictionary<string, string> _signatureCache = new();
        private readonly object[] _entityLocks = Enumerable.Range(0, 256).Select(_ => new object()).ToArray();

        private object GetEntityLock(int entityId) => _entityLocks[(uint)entityId % _entityLocks.Length];

        public void AddEntity(IGameObject entity)
        {
            lock (GetEntityLock(entity.Id))
            {
                _entityComponents[entity.Id] = new Dictionary<Type, IComponent>();
                MoveToArchetypeInternal(entity.Id);
            }
        }

        public void RemoveEntity(int entityId)
        {
            lock (GetEntityLock(entityId))
            {
                if (_entityToArchetype.TryRemove(entityId, out var archetype))
                {
                    archetype.RemoveEntity(entityId);
                }
                _entityComponents.TryRemove(entityId, out _);
            }
        }

        public void AddComponent<T>(IGameObject entity, T component) where T : class, IComponent
        {
            AddComponent(entity, (IComponent)component);
        }

        public void AddComponent(IGameObject entity, IComponent component)
        {
            var componentType = component.GetType();
            lock (GetEntityLock(entity.Id))
            {
                var components = _entityComponents.GetOrAdd(entity.Id, _ => new Dictionary<Type, IComponent>());
                components[componentType] = component;
                component.Owner = entity;
                component.Initialize();

                if (_entityToArchetype.TryGetValue(entity.Id, out var currentArchetype) &&
                    currentArchetype.AddTransitions.TryGetValue(componentType, out var targetArchetype))
                {
                    currentArchetype.RemoveEntity(entity.Id);
                    targetArchetype.AddEntity(entity.Id, components);
                    _entityToArchetype[entity.Id] = targetArchetype;
                }
                else
                {
                    MoveToArchetypeInternal(entity.Id);
                    // Build transition edge
                    if (_entityToArchetype.TryGetValue(entity.Id, out var newArchetype) && currentArchetype != null)
                    {
                        currentArchetype.AddTransitions.TryAdd(componentType, newArchetype);
                        newArchetype.RemoveTransitions.TryAdd(componentType, currentArchetype);
                    }
                }
            }
        }

        public void RemoveComponent<T>(IGameObject entity) where T : class, IComponent
        {
            RemoveComponent(entity, typeof(T));
        }

        public void RemoveComponent(IGameObject entity, Type componentType)
        {
            lock (GetEntityLock(entity.Id))
            {
                if (_entityComponents.TryGetValue(entity.Id, out var components))
                {
                    if (components.Remove(componentType, out var component))
                    {
                        component.Shutdown();
                        component.Owner = null;

                        if (_entityToArchetype.TryGetValue(entity.Id, out var currentArchetype) &&
                            currentArchetype.RemoveTransitions.TryGetValue(componentType, out var targetArchetype))
                        {
                            currentArchetype.RemoveEntity(entity.Id);
                            targetArchetype.AddEntity(entity.Id, components);
                            _entityToArchetype[entity.Id] = targetArchetype;
                        }
                        else
                        {
                            var oldArchetype = currentArchetype;
                            MoveToArchetypeInternal(entity.Id);
                            // Build transition edge
                            if (_entityToArchetype.TryGetValue(entity.Id, out var newArchetype) && oldArchetype != null)
                            {
                                oldArchetype.RemoveTransitions.TryAdd(componentType, newArchetype);
                                newArchetype.AddTransitions.TryAdd(componentType, oldArchetype);
                            }
                        }
                    }
                }
            }
        }

        private void MoveToArchetypeInternal(int entityId)
        {
            if (!_entityComponents.TryGetValue(entityId, out var components)) return;

            if (components.Count == 0)
            {
                if (_entityToArchetype.TryRemove(entityId, out var oldArch))
                {
                    oldArch.RemoveEntity(entityId);
                }
                _entityComponents.TryRemove(entityId, out _);
                return;
            }

            // High-performance signature generation
            var sortedTypes = components.Keys.OrderBy(t => t.FullName!).ToList();
            var typeIds = string.Join("+", sortedTypes.Select(t => t.FullName));
            var signatureKey = _signatureCache.GetOrAdd(typeIds, typeIds);

            // Find or create archetype
            Archetype targetArchetype;
            lock (_archetypes)
            {
                if (!_signatureToArchetype.TryGetValue(signatureKey, out targetArchetype!))
                {
                    targetArchetype = new Archetype(sortedTypes);
                    _archetypes.Add(targetArchetype);
                    _signatureToArchetype[signatureKey] = targetArchetype;

                    foreach (var type in sortedTypes)
                    {
                        _typeToArchetypesCache.AddOrUpdate(type,
                            _ => new[] { targetArchetype },
                            (_, existing) =>
                            {
                                var updated = new Archetype[existing.Length + 1];
                                System.Array.Copy(existing, updated, existing.Length);
                                updated[existing.Length] = targetArchetype;
                                return updated;
                            });
                    }
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
            return GetComponent(entityId, typeof(T)) as T;
        }

        public IComponent? GetComponent(int entityId, Type componentType)
        {
            if (_entityToArchetype.TryGetValue(entityId, out var archetype))
            {
                return archetype.GetComponent(entityId, componentType);
            }
            return null;
        }

        public IEnumerable<T> GetComponents<T>() where T : class, IComponent
        {
            if (_typeToArchetypesCache.TryGetValue(typeof(T), out var targetArchetypes))
            {
                foreach (var archetype in targetArchetypes)
                {
                    foreach (var component in archetype.GetComponents<T>())
                    {
                        yield return component;
                    }
                }
            }
        }

        public IEnumerable<ArchetypeChunk<T>> GetChunks<T>() where T : class, IComponent
        {
            if (_typeToArchetypesCache.TryGetValue(typeof(T), out var targetArchetypes))
            {
                foreach (var archetype in targetArchetypes)
                {
                    yield return archetype.GetChunk<T>();
                }
            }
        }

        public IEnumerable<IComponent> GetComponents(Type componentType)
        {
            if (_typeToArchetypesCache.TryGetValue(componentType, out var targetArchetypes))
            {
                foreach (var archetype in targetArchetypes)
                {
                    foreach (var component in archetype.GetComponents(componentType))
                    {
                        yield return component;
                    }
                }
            }
        }

        public IEnumerable<IComponent> GetAllComponents(int entityId)
        {
            if (_entityComponents.TryGetValue(entityId, out var components))
            {
                return components.Values;
            }
            return Enumerable.Empty<IComponent>();
        }

        public IEnumerable<Archetype> GetArchetypesWithComponents(params Type[] componentTypes)
        {
            if (componentTypes.Length == 0) return Enumerable.Empty<Archetype>();

            HashSet<Archetype>? results = null;

            foreach (var type in componentTypes)
            {
                if (_typeToArchetypesCache.TryGetValue(type, out var archetypes))
                {
                    if (results == null)
                    {
                        results = new HashSet<Archetype>(archetypes);
                    }
                    else
                    {
                        results.IntersectWith(archetypes);
                    }
                }
                else
                {
                    return Enumerable.Empty<Archetype>();
                }
            }

            return results ?? Enumerable.Empty<Archetype>();
        }

        public void Compact()
        {
            lock (_archetypes)
            {
                Parallel.ForEach(_archetypes, archetype =>
                {
                    archetype.Compact();
                });
            }
        }
    }
}
