using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Shared.Interfaces;

namespace Shared.Models
{
    /// <summary>
    /// Stores entities with the exact same component composition in contiguous memory.
    /// </summary>
    public class Archetype
    {
        private readonly List<int> _entityIds = new();
        private readonly Dictionary<int, int> _entityIdToIndex = new();
        private readonly Dictionary<Type, IList> _componentArrays = new();
        public HashSet<Type> Signature { get; }

        public Archetype(IEnumerable<Type> signature)
        {
            Signature = new HashSet<Type>(signature);
            foreach (var type in Signature)
            {
                var listType = typeof(List<>).MakeGenericType(type);
                _componentArrays[type] = (IList)Activator.CreateInstance(listType)!;
            }
        }

        public int EntityCount => _entityIds.Count;

        public void AddEntity(int entityId, IDictionary<Type, IComponent> components)
        {
            _entityIdToIndex[entityId] = _entityIds.Count;
            _entityIds.Add(entityId);
            foreach (var type in Signature)
            {
                _componentArrays[type].Add(components[type]);
            }
        }

        public void RemoveEntity(int entityId)
        {
            if (!_entityIdToIndex.TryGetValue(entityId, out int index)) return;

            int lastIndex = _entityIds.Count - 1;
            if (index != lastIndex)
            {
                int lastEntityId = _entityIds[lastIndex];
                _entityIds[index] = lastEntityId;
                _entityIdToIndex[lastEntityId] = index;

                foreach (var array in _componentArrays.Values)
                {
                    array[index] = array[lastIndex];
                }
            }

            _entityIdToIndex.Remove(entityId);
            _entityIds.RemoveAt(lastIndex);
            foreach (var array in _componentArrays.Values)
            {
                array.RemoveAt(lastIndex);
            }
        }

        private static class EmptyList<T>
        {
            public static readonly List<T> Instance = new();
        }

        internal List<T> GetComponentsInternal<T>() where T : class, IComponent
        {
            if (_componentArrays.TryGetValue(typeof(T), out var list))
            {
                return (List<T>)list;
            }
            return EmptyList<T>.Instance;
        }

        internal IList GetComponentsInternal(Type type)
        {
            if (_componentArrays.TryGetValue(type, out var list))
            {
                return list;
            }
            return Array.Empty<IComponent>();
        }

        public IEnumerable<T> GetComponents<T>() where T : class, IComponent
        {
            return GetComponentsInternal<T>();
        }

        public IEnumerable<IComponent> GetComponents(Type type)
        {
            var list = GetComponentsInternal(type);
            foreach (var item in list) yield return (IComponent)item!;
        }

        public bool ContainsEntity(int entityId) => _entityIdToIndex.ContainsKey(entityId);

        public IComponent? GetComponent(int entityId, Type type)
        {
            if (_entityIdToIndex.TryGetValue(entityId, out int index) && _componentArrays.TryGetValue(type, out var array))
            {
                return (IComponent)array[index]!;
            }
            return null;
        }

        private static readonly ConcurrentDictionary<Type, Action<IList>> _trimDelegates = new();

        public void Compact()
        {
            // If capacity is significantly larger than count, trim it
            foreach (var array in _componentArrays.Values)
            {
                var type = array.GetType();
                var trim = _trimDelegates.GetOrAdd(type, t =>
                {
                    var method = t.GetMethod("TrimExcess")!;
                    var param = Expression.Parameter(typeof(IList), "list");
                    var cast = Expression.Convert(param, t);
                    var call = Expression.Call(cast, method);
                    return Expression.Lambda<Action<IList>>(call, param).Compile();
                });
                trim(array);
            }
            _entityIds.TrimExcess();
        }

        public IEnumerable<int> GetEntityIds() => _entityIds;
    }
}
