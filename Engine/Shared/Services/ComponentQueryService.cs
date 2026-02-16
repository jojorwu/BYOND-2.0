using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;

namespace Shared.Services
{
    public class ComponentQueryService : IComponentQueryService
    {
        private readonly IComponentManager _componentManager;
        private readonly ConcurrentDictionary<Type, List<(Action<ComponentEventArgs> Added, Action<ComponentEventArgs> Removed)>> _subscriptions = new();
        private readonly ConcurrentDictionary<string, HashSet<IGameObject>> _queryCache = new();
        private readonly ConcurrentDictionary<string, Type[]> _cacheKeyToTypes = new();
        private readonly ConcurrentDictionary<Type, List<string>> _typeToCacheKeys = new();

        public ComponentQueryService(IComponentManager componentManager)
        {
            _componentManager = componentManager;
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

            var key = GetCacheKey(componentTypes);
            if (_queryCache.TryGetValue(key, out var cached))
            {
                lock (cached) return cached.ToList();
            }

            // Perform full query and cache result
            var results = PerformFullQuery(componentTypes);
            var set = new HashSet<IGameObject>(results);
            if (_queryCache.TryAdd(key, set))
            {
                _cacheKeyToTypes[key] = componentTypes.ToArray();
                foreach (var type in componentTypes)
                {
                    _typeToCacheKeys.AddOrUpdate(type, _ => new List<string> { key }, (_, list) => { lock (list) { list.Add(key); } return list; });
                }
            }
            return set.ToList();
        }

        private string GetCacheKey(Type[] types)
        {
            if (types.Length == 1) return types[0].FullName!;
            var names = types.Select(t => t.FullName).OrderBy(n => n).ToArray();
            return string.Join("+", names);
        }

        private IEnumerable<IGameObject> PerformFullQuery(Type[] componentTypes)
        {
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
                List<string> keysCopy;
                lock (keys) keysCopy = keys.ToList();

                foreach (var key in keysCopy)
                {
                    if (_queryCache.TryGetValue(key, out var set) && _cacheKeyToTypes.TryGetValue(key, out var types))
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
                            lock (set) set.Add(e.Owner);
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
                List<string> keysCopy;
                lock (keys) keysCopy = keys.ToList();

                foreach (var key in keysCopy)
                {
                    if (_queryCache.TryGetValue(key, out var set))
                    {
                        lock (set) set.Remove(e.Owner);
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
