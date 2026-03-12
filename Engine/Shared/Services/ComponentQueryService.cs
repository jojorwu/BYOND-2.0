using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services;
    public class ComponentQueryService : IComponentQueryService
    {
        private class QueryResult : IEntityQuery
        {
            public readonly List<Archetype> Archetypes = new();
            public readonly object Lock = new();
            private readonly IGameState? _gameState;

            public QueryResult(IGameState? gameState)
            {
                _gameState = gameState;
            }

            public IReadOnlyList<IGameObject> Snapshot => BuildSnapshot();

            public IEnumerable<Archetype> GetMatchingArchetypes()
            {
                lock (Lock)
                {
                    return Archetypes.ToList();
                }
            }

            private IReadOnlyList<IGameObject> BuildSnapshot()
            {
                var results = new List<IGameObject>();
                lock (Lock)
                {
                    foreach (var arch in Archetypes)
                    {
                        results.AddRange(arch.GetEntitiesSnapshot());
                    }
                }
                return results;
            }

            public IEnumerator<IGameObject> GetEnumerator()
            {
                List<Archetype> archetypesCopy;
                lock (Lock) archetypesCopy = Archetypes.ToList();

                foreach (var arch in archetypesCopy)
                {
                    foreach (var entity in arch.GetEntitiesSnapshot())
                    {
                        yield return entity;
                    }
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private readonly IComponentManager _componentManager;
        private readonly IGameState? _gameState;
        private readonly ConcurrentDictionary<Type, List<(Action<ComponentEventArgs> Added, Action<ComponentEventArgs> Removed)>> _subscriptions = new();
        private readonly ConcurrentDictionary<ComponentSignature, QueryResult> _queryCache = new();
        private readonly ConcurrentDictionary<ComponentSignature, Type[]> _cacheKeyToTypes = new();

        public ComponentQueryService(IComponentManager componentManager, IGameState? gameState = null)
        {
            _componentManager = componentManager;
            _gameState = gameState;

            if (_componentManager is ComponentManager cm && cm.ArchetypeManager is ArchetypeManager am)
            {
                am.ArchetypeCreated += OnArchetypeCreated;
            }
        }

        public IEnumerable<IGameObject> Query<T>() where T : class, IComponent
        {
            return Query(typeof(T));
        }

        public IEnumerable<IGameObject> Query(params Type[] componentTypes)
        {
            return GetQuery(componentTypes);
        }

        public IEntityQuery GetQuery(params Type[] componentTypes)
        {
            if (componentTypes == null || componentTypes.Length == 0)
                return new QueryResult(_gameState); // Empty

            var key = new ComponentSignature(componentTypes);
            if (_queryCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var queryResult = new QueryResult(_gameState);
            _cacheKeyToTypes[key] = componentTypes.ToArray();

            // Initial population
            if (_componentManager is ComponentManager cm && cm.ArchetypeManager is ArchetypeManager am)
            {
                var matchingArchetypes = am.GetArchetypesWithComponents(componentTypes);
                lock (queryResult.Lock)
                {
                    queryResult.Archetypes.AddRange(matchingArchetypes);
                }
            }

            if (_queryCache.TryAdd(key, queryResult))
            {
                return queryResult;
            }

            return _queryCache[key];
        }

        private void OnArchetypeCreated(object? sender, Archetype archetype)
        {
            foreach (var kvp in _queryCache)
            {
                var key = kvp.Key;
                var queryResult = kvp.Value;

                if (archetype.Signature.Mask.ContainsAll(key.Mask))
                {
                    lock (queryResult.Lock)
                    {
                        queryResult.Archetypes.Add(archetype);
                    }
                }
            }
        }

        private int GetCount(Type t)
        {
            var results = _componentManager.GetComponents(t);
            int count = 0;
            foreach (var _ in results) count++;
            return count;
        }

        private IEnumerable<IGameObject> GetOwners(Type t)
        {
            var results = _componentManager.GetComponents(t);
            return results.Select(c => c.Owner).Where(o => o != null)!;
        }

        public void Subscribe<T>(Action<ComponentEventArgs> onAdded, Action<ComponentEventArgs> onRemoved) where T : class, IComponent
        {
            var list = _subscriptions.GetOrAdd(typeof(T), _ => new List<(Action<ComponentEventArgs>, Action<ComponentEventArgs>)>());
            lock (list)
            {
                list.Add((onAdded, onRemoved));
            }
        }

        public void Unsubscribe<T>(Action<ComponentEventArgs> onAdded, Action<ComponentEventArgs> onRemoved) where T : class, IComponent
        {
            if (_subscriptions.TryGetValue(typeof(T), out var list))
            {
                lock (list)
                {
                    list.Remove((onAdded, onRemoved));
                }
            }
        }

        private void OnComponentAdded(object? sender, ComponentEventArgs e)
        {
            if (_subscriptions.TryGetValue(e.ComponentType, out var list))
            {
                List<(Action<ComponentEventArgs> Added, Action<ComponentEventArgs> Removed)> copy;
                lock (list) copy = list.ToList();
                foreach (var sub in copy) sub.Added(e);
            }
        }

        private void OnComponentRemoved(object? sender, ComponentEventArgs e)
        {
            if (_subscriptions.TryGetValue(e.ComponentType, out var list))
            {
                List<(Action<ComponentEventArgs> Added, Action<ComponentEventArgs> Removed)> copy;
                lock (list) copy = list.ToList();
                foreach (var sub in copy) sub.Removed(e);
            }
        }
    }
