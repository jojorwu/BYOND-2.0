using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services;
    public class ComponentQueryService : EngineService, IComponentQueryService, IDisposable, IShrinkable
    {
        private readonly ConcurrentQueue<IGameObject[]> _replacedArrays = new();

        public void Shrink()
        {
            while (_replacedArrays.TryDequeue(out var array))
            {
                ArrayPool<IGameObject>.Shared.Return(array, true);
            }
        }

        private class ReadOnlySpanWrapper<T> : IReadOnlyList<T>
        {
            private readonly T[] _array;
            private readonly int _count;

            public ReadOnlySpanWrapper(T[] array, int count)
            {
                _array = array;
                _count = count;
            }

            public int Count => _count;
            public T this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)_count) throw new IndexOutOfRangeException();
                    return _array[index];
                }
            }

            public Enumerator GetEnumerator() => new Enumerator(_array, _count);
            IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

            public struct Enumerator : IEnumerator<T>
            {
                private readonly T[] _array;
                private readonly int _count;
                private int _index;

                public Enumerator(T[] array, int count)
                {
                    _array = array;
                    _count = count;
                    _index = -1;
                }

                public bool MoveNext() => ++_index < _count;
                public T Current => _array[_index];
                object System.Collections.IEnumerator.Current => Current!;
                public void Reset() => _index = -1;
                public void Dispose() { }
            }
        }

        private class QueryResult : IEntityQuery, IDisposable
        {
            public readonly ComponentMask Mask;
            private readonly ComponentQueryService _parent;
            private volatile Archetype[] _archetypes = Array.Empty<Archetype>();
            private readonly System.Threading.Lock _lock = new();
            private readonly IGameState? _gameState;
            private IGameObject[]? _cachedSnapshot;
            private long _version = 0;
            private long _snapshotVersion = -1;

            public QueryResult(ComponentQueryService parent, IGameState? gameState, ComponentMask mask)
            {
                _parent = parent;
                _gameState = gameState;
                Mask = mask;
            }

            public IReadOnlyList<IGameObject> Snapshot => BuildSnapshot();
            public long Version => Interlocked.Read(ref _version);

            public void AddArchetype(Archetype archetype)
            {
                using (_lock.EnterScope())
                {
                    if (_archetypes.Contains(archetype)) return;
                    var updated = new Archetype[_archetypes.Length + 1];
                    Array.Copy(_archetypes, updated, _archetypes.Length);
                    updated[_archetypes.Length] = archetype;
                    _archetypes = updated;
                    InvalidateSnapshot();
                    Interlocked.Increment(ref _version);
                }
            }

            public void AddArchetypes(IEnumerable<Archetype> matching)
            {
                using (_lock.EnterScope())
                {
                    var matchingArray = matching.Where(a => !_archetypes.Contains(a)).ToArray();
                    if (matchingArray.Length == 0) return;

                    var updated = new Archetype[_archetypes.Length + matchingArray.Length];
                    Array.Copy(_archetypes, updated, _archetypes.Length);
                    Array.Copy(matchingArray, 0, updated, _archetypes.Length, matchingArray.Length);
                    _archetypes = updated;
                    InvalidateSnapshot();
                    Interlocked.Increment(ref _version);
                }
            }

            private void InvalidateSnapshot()
            {
                if (_cachedSnapshot != null)
                {
                    _parent._replacedArrays.Enqueue(_cachedSnapshot);
                    _cachedSnapshot = null;
                }
                _snapshotVersion = -1;
            }

            public IEnumerable<Archetype> GetMatchingArchetypes() => _archetypes;

            private IReadOnlyList<IGameObject> BuildSnapshot()
            {
                long currentVersion = Version;
                if (_cachedSnapshot != null && _snapshotVersion == currentVersion) return _cachedSnapshot;

                using (_lock.EnterScope())
                {
                    if (_cachedSnapshot != null && _snapshotVersion == currentVersion) return _cachedSnapshot;

                    var archetypes = _archetypes;
                    int totalCount = 0;
                    for (int i = 0; i < archetypes.Length; i++) totalCount += archetypes[i].EntityCount;

                    if (_cachedSnapshot != null)
                    {
                        _parent._replacedArrays.Enqueue(_cachedSnapshot);
                    }

                    var results = ArrayPool<IGameObject>.Shared.Rent(totalCount);
                    int offset = 0;
                    for (int i = 0; i < archetypes.Length; i++)
                    {
                        var arch = archetypes[i];
                        arch.CopyEntitiesTo(results, offset);
                        offset += arch.EntityCount;
                    }

                    // Zero out the rest of the rented array to ensure clean snapshot
                    if (results.Length > totalCount)
                    {
                        Array.Clear(results, totalCount, results.Length - totalCount);
                    }

                    _cachedSnapshot = results;
                    _snapshotVersion = currentVersion;
                    return new ReadOnlySpanWrapper<IGameObject>(results, totalCount);
                }
            }

            public void Dispose()
            {
                using (_lock.EnterScope())
                {
                    InvalidateSnapshot();
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
        private readonly ConcurrentDictionary<int, QueryList> _queriesByComponent = new();

        private class QueryList
        {
            public readonly System.Threading.Lock Lock = new();
            public readonly List<QueryResult> Items = new();
        }

        public ComponentQueryService(IComponentManager componentManager, IArchetypeManager archetypeManager, IGameState? gameState = null)
        {
            _componentManager = componentManager;
            _archetypeManager = archetypeManager;
            _gameState = gameState;

            _archetypeManager.ArchetypeCreated += OnArchetypeCreated;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _archetypeManager.ArchetypeCreated -= OnArchetypeCreated;
            foreach (var query in _queryCache.Values)
            {
                query.Dispose();
            }
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
                return new QueryResult(this, _gameState, default);

            var key = new ComponentSignature(componentTypes);
            if (_queryCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var queryResult = new QueryResult(this, _gameState, key.Mask);

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
                    var list = _queriesByComponent.GetOrAdd(componentId, _ => new QueryList());
                    using (list.Lock.EnterScope())
                    {
                        list.Items.Add(queryResult);
                    }
                }
                return queryResult;
            }

            return _queryCache[key];
        }

        private void OnArchetypeCreated(object? sender, Archetype archetype)
        {
            var archetypeMask = archetype.Signature.Mask;
            var bits = archetypeMask.GetSetBits();

            // Iterate through all component IDs in the new archetype.
            // Since each query is registered under exactly one component ID (the first one in its mask),
            // this loop will find every matching query exactly once.
            while (bits.MoveNext())
            {
                int id = bits.Current;
                if (_queriesByComponent.TryGetValue(id, out var queryList))
                {
                    using (queryList.Lock.EnterScope())
                    {
                        var items = queryList.Items;
                        for (int i = 0; i < items.Count; i++)
                        {
                            var query = items[i];
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
