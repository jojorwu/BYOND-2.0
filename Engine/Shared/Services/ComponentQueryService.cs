using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services;
    public class ComponentQueryService : EngineService, IComponentQueryService, IDisposable
    {
        private class QueryResult : IEntityQuery
        {
            public readonly ComponentMask Mask;
            private volatile Archetype[] _archetypes = Array.Empty<Archetype>();
            private readonly System.Threading.Lock _lock = new();
            private readonly IGameState? _gameState;
            private IGameObject[]? _cachedSnapshot;
            private long _version = 0;

            public QueryResult(IGameState? gameState, ComponentMask mask)
            {
                _gameState = gameState;
                Mask = mask;
            }

            public IReadOnlyList<IGameObject> Snapshot => BuildSnapshot();
            public long Version => Interlocked.Read(ref _version);

            public void AddArchetype(Archetype archetype)
            {
                lock (_lock)
                {
                    if (_archetypes.Contains(archetype)) return;
                    var updated = new Archetype[_archetypes.Length + 1];
                    Array.Copy(_archetypes, updated, _archetypes.Length);
                    updated[_archetypes.Length] = archetype;
                    _archetypes = updated;
                    _cachedSnapshot = null;
                    Interlocked.Increment(ref _version);
                }
            }

            public void AddArchetypes(IEnumerable<Archetype> matching)
            {
                lock (_lock)
                {
                    var matchingArray = matching.Where(a => !_archetypes.Contains(a)).ToArray();
                    if (matchingArray.Length == 0) return;

                    var updated = new Archetype[_archetypes.Length + matchingArray.Length];
                    Array.Copy(_archetypes, updated, _archetypes.Length);
                    Array.Copy(matchingArray, 0, updated, _archetypes.Length, matchingArray.Length);
                    _archetypes = updated;
                    _cachedSnapshot = null;
                    Interlocked.Increment(ref _version);
                }
            }

            public IEnumerable<Archetype> GetMatchingArchetypes() => _archetypes;

            private IReadOnlyList<IGameObject> BuildSnapshot()
            {
                var archetypes = _archetypes;
                var snapshot = _cachedSnapshot;
                if (snapshot != null) return snapshot;

                lock (_lock)
                {
                    if (_cachedSnapshot != null) return _cachedSnapshot;

                    int totalCount = 0;
                    foreach (var arch in archetypes) totalCount += arch.EntityCount;

                    var results = new IGameObject[totalCount];
                    int offset = 0;
                    foreach (var arch in archetypes)
                    {
                        arch.CopyEntitiesTo(results, offset);
                        offset += arch.EntityCount;
                    }
                    _cachedSnapshot = results;
                    return results;
                }
            }

            public QueryEnumerator GetEnumerator()
            {
                return new QueryEnumerator(_archetypes);
            }

            IEnumerator<IGameObject> IEnumerable<IGameObject>.GetEnumerator() => GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
            public struct QueryEnumerator : IEnumerator<IGameObject>
            {
                private readonly Archetype[] _archetypes;
                private int _archetypeIndex;
                private Archetype.EntityEnumerator _entityEnumerator;

                public QueryEnumerator(Archetype[] archetypes)
                {
                    _archetypes = archetypes;
                    _archetypeIndex = 0;
                    _entityEnumerator = archetypes.Length > 0 ? archetypes[0].GetEntities() : default;
                }

                public bool MoveNext()
                {
                    while (true)
                    {
                        if (_entityEnumerator.MoveNext()) return true;

                        if (++_archetypeIndex >= _archetypes.Length) return false;
                        _entityEnumerator = _archetypes[_archetypeIndex].GetEntities();
                    }
                }

                public IGameObject Current => _entityEnumerator.Current;
                object System.Collections.IEnumerator.Current => Current;
                public void Reset()
                {
                    _archetypeIndex = 0;
                    _entityEnumerator = _archetypes.Length > 0 ? _archetypes[0].GetEntities() : default;
                }
                public void Dispose() { }
            }
        }

        private readonly IComponentManager _componentManager;
        private readonly IArchetypeManager _archetypeManager;
        private readonly IGameState? _gameState;
        private readonly ConcurrentDictionary<Type, (Action<ComponentEventArgs> Added, Action<ComponentEventArgs> Removed)[]> _subscriptions = new();
        private readonly ConcurrentDictionary<ComponentSignature, QueryResult> _queryCache = new();
        private readonly ConcurrentDictionary<int, List<QueryResult>> _queriesByComponent = new();

        public ComponentQueryService(IComponentManager componentManager, IArchetypeManager archetypeManager, IGameState? gameState = null)
        {
            _componentManager = componentManager;
            _archetypeManager = archetypeManager;
            _gameState = gameState;

            _archetypeManager.ArchetypeCreated += OnArchetypeCreated;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return base.StopAsync(cancellationToken);
        }

        public void Dispose()
        {
            _archetypeManager.ArchetypeCreated -= OnArchetypeCreated;
            _queryCache.Clear();
            _subscriptions.Clear();
            _queriesByComponent.Clear();
            GC.SuppressFinalize(this);
        }

        public IEnumerable<IGameObject> Query<T>() where T : class, IComponent
        {
            return Query(typeof(T));
        }

        public IEnumerable<IGameObject> Query(params ReadOnlySpan<Type> componentTypes)
        {
            return GetQuery(componentTypes);
        }

        public IEntityQuery GetQuery(params ReadOnlySpan<Type> componentTypes)
        {
            if (componentTypes.IsEmpty)
                return new QueryResult(_gameState, default);

            var key = new ComponentSignature(componentTypes);
            if (_queryCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var queryResult = new QueryResult(_gameState, key.Mask);

            // Initial population
            var matchingArchetypes = _archetypeManager.GetArchetypesWithComponents(componentTypes);
            queryResult.AddArchetypes(matchingArchetypes);

            if (_queryCache.TryAdd(key, queryResult))
            {
                // Register for faster lookup during archetype creation
                var setBits = key.Mask.GetSetBits();
                if (setBits.MoveNext())
                {
                    int componentId = setBits.Current;
                    _queriesByComponent.AddOrUpdate(componentId,
                        _ => new List<QueryResult> { queryResult },
                        (_, list) => { lock (list) { list.Add(queryResult); } return list; });
                }
                return queryResult;
            }

            return _queryCache[key];
        }

        private void OnArchetypeCreated(object? sender, Archetype archetype)
        {
            var archetypeMask = archetype.Signature.Mask;
            var checkedQueries = new HashSet<QueryResult>();

            var bits = archetypeMask.GetSetBits();
            while (bits.MoveNext())
            {
                int componentId = bits.Current;
                if (_queriesByComponent.TryGetValue(componentId, out var queries))
                {
                    QueryResult[] snapshot;
                    lock (queries)
                    {
                        if (queries.Count == 0) continue;
                        snapshot = queries.ToArray();
                    }

                    for (int i = 0; i < snapshot.Length; i++)
                    {
                        var query = snapshot[i];
                        if (checkedQueries.Add(query))
                        {
                            if (archetypeMask.ContainsAll(query.Mask))
                            {
                                query.AddArchetype(archetype);
                            }
                        }
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
            _subscriptions.AddOrUpdate(typeof(T),
                _ => new[] { (onAdded, onRemoved) },
                (_, existing) =>
                {
                    var updated = new (Action<ComponentEventArgs>, Action<ComponentEventArgs>)[existing.Length + 1];
                    Array.Copy(existing, updated, existing.Length);
                    updated[existing.Length] = (onAdded, onRemoved);
                    return updated;
                });
        }

        public void Unsubscribe<T>(Action<ComponentEventArgs> onAdded, Action<ComponentEventArgs> onRemoved) where T : class, IComponent
        {
            _subscriptions.AddOrUpdate(typeof(T),
                _ => Array.Empty<(Action<ComponentEventArgs>, Action<ComponentEventArgs>)>(),
                (_, existing) =>
                {
                    int index = Array.IndexOf(existing, (onAdded, onRemoved));
                    if (index == -1) return existing;

                    if (existing.Length == 1) return Array.Empty<(Action<ComponentEventArgs>, Action<ComponentEventArgs>)>();

                    var updated = new (Action<ComponentEventArgs>, Action<ComponentEventArgs>)[existing.Length - 1];
                    if (index > 0) Array.Copy(existing, 0, updated, 0, index);
                    if (index < existing.Length - 1) Array.Copy(existing, index + 1, updated, index, existing.Length - index - 1);
                    return updated;
                });
        }

        private void OnComponentAdded(object? sender, ComponentEventArgs e)
        {
            if (_subscriptions.TryGetValue(e.ComponentType, out var handlers))
            {
                for (int i = 0; i < handlers.Length; i++)
                {
                    handlers[i].Added(e);
                }
            }
        }

        private void OnComponentRemoved(object? sender, ComponentEventArgs e)
        {
            if (_subscriptions.TryGetValue(e.ComponentType, out var handlers))
            {
                for (int i = 0; i < handlers.Length; i++)
                {
                    handlers[i].Removed(e);
                }
            }
        }

        public override Dictionary<string, object> GetDiagnosticInfo()
        {
            var info = base.GetDiagnosticInfo();
            info["QueryCacheSize"] = _queryCache.Count;
            info["SubscriptionCount"] = _subscriptions.Count;
            return info;
        }
    }
