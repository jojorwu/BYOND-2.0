using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services
{
    public class ComponentQueryService : IComponentQueryService
    {
        private class QueryResult
        {
            public readonly HashSet<IGameObject> Set = new();
            public volatile IGameObject[] Snapshot = System.Array.Empty<IGameObject>();
            public readonly object Lock = new();

            public void Update(IGameObject obj, bool add)
            {
                lock (Lock)
                {
                    if (add)
                    {
                        if (Set.Add(obj)) Snapshot = Set.ToArray();
                    }
                    else
                    {
                        if (Set.Remove(obj)) Snapshot = Set.ToArray();
                    }
                }
            }

            public void Initialize(IEnumerable<IGameObject> objects)
            {
                lock (Lock)
                {
                    foreach (var obj in objects) Set.Add(obj);
                    Snapshot = Set.ToArray();
                }
            }
        }

        private readonly IComponentManager _componentManager;
        private readonly IGameState? _gameState;
        private readonly ConcurrentDictionary<Type, List<(Action<ComponentEventArgs> Added, Action<ComponentEventArgs> Removed)>> _subscriptions = new();
        private readonly ConcurrentDictionary<ComponentSignature, QueryResult> _queryCache = new();
        private readonly ConcurrentDictionary<ComponentSignature, Type[]> _cacheKeyToTypes = new();
        private readonly ConcurrentDictionary<Type, List<ComponentSignature>> _typeToCacheKeys = new();

        public ComponentQueryService(IComponentManager componentManager, IGameState? gameState = null)
        {
            _componentManager = componentManager;
            _gameState = gameState;
            _componentManager.ComponentAdded += OnComponentAdded;
            _componentManager.ComponentRemoved += OnComponentRemoved;
        }

        public IEnumerable<IGameObject> Query<T>() where T : class, IComponent
        {
            return Query(typeof(T));
        }

        public IEnumerable<IGameObject> Query(params Type[] componentTypes)
        {
            if (componentTypes == null || componentTypes.Length == 0)
                return Enumerable.Empty<IGameObject>();

            if (componentTypes.Length == 1)
            {
                var type = componentTypes[0];
                return _componentManager.GetComponents(type).Select(c => c.Owner).Where(o => o != null)!;
            }

            var key = new ComponentSignature(componentTypes);
            if (_queryCache.TryGetValue(key, out var cached))
            {
                return cached.Snapshot;
            }

            // Perform full query and cache result
            var results = PerformFullQuery(componentTypes);
            var queryResult = new QueryResult();
            queryResult.Initialize(results);

            if (_queryCache.TryAdd(key, queryResult))
            {
                _cacheKeyToTypes[key] = componentTypes.ToArray();
                foreach (var type in componentTypes)
                {
                    _typeToCacheKeys.AddOrUpdate(type, _ => new List<ComponentSignature> { key }, (_, list) => { lock (list) { list.Add(key); } return list; });
                }
                return queryResult.Snapshot;
            }

            return _queryCache[key].Snapshot;
        }

        private IEnumerable<IGameObject> PerformFullQuery(Type[] componentTypes)
        {
            // High-performance archetype-based intersection
            if (_componentManager is ComponentManager cm && cm.ArchetypeManager is ArchetypeManager am && _gameState != null)
            {
                var archetypes = am.GetArchetypesWithComponents(componentTypes);
                var results = new List<IGameObject>();
                foreach (var arch in archetypes)
                {
                    var ids = arch.GetEntityIdsSnapshot();
                    foreach (int id in ids)
                    {
                        if (_gameState.GameObjects.TryGetValue(id, out var obj))
                        {
                            results.Add(obj);
                        }
                    }
                }
                return results;
            }

            // Fallback for missing GameState
            var smallestSet = componentTypes
                .Select(t => (Type: t, Count: GetCount(t)))
                .OrderBy(x => x.Count)
                .First();

            var candidates = GetOwners(smallestSet.Type);

            foreach (var type in componentTypes.Where(t => t != smallestSet.Type))
            {
                var ownersOfType = new HashSet<int>(GetOwners(type).Select(o => o.Id));
                candidates = candidates.Where(o => ownersOfType.Contains(o.Id));
            }

            return candidates;
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
            if (_typeToCacheKeys.TryGetValue(e.ComponentType, out var keys))
            {
                List<ComponentSignature> keysCopy;
                lock (keys) keysCopy = keys.ToList();

                foreach (var key in keysCopy)
                {
                    if (_queryCache.TryGetValue(key, out var result) && _cacheKeyToTypes.TryGetValue(key, out var types))
                    {
                        // Check if entity now has all components for this query
                        bool hasAll = true;
                        foreach (var t in types)
                        {
                            if (_componentManager.GetComponent(e.Owner, t) == null)
                            {
                                hasAll = false;
                                break;
                            }
                        }

                        if (hasAll)
                        {
                            result.Update(e.Owner, true);
                        }
                    }
                }
            }

            if (_subscriptions.TryGetValue(e.ComponentType, out var list))
            {
                List<(Action<ComponentEventArgs> Added, Action<ComponentEventArgs> Removed)> copy;
                lock (list) copy = list.ToList();
                foreach (var sub in copy) sub.Added(e);
            }
        }

        private void OnComponentRemoved(object? sender, ComponentEventArgs e)
        {
            if (_typeToCacheKeys.TryGetValue(e.ComponentType, out var keys))
            {
                List<ComponentSignature> keysCopy;
                lock (keys) keysCopy = keys.ToList();

                foreach (var key in keysCopy)
                {
                    if (_queryCache.TryGetValue(key, out var result))
                    {
                        result.Update(e.Owner, false);
                    }
                }
            }

            if (_subscriptions.TryGetValue(e.ComponentType, out var list))
            {
                List<(Action<ComponentEventArgs> Added, Action<ComponentEventArgs> Removed)> copy;
                lock (list) copy = list.ToList();
                foreach (var sub in copy) sub.Removed(e);
            }
        }
    }
}
