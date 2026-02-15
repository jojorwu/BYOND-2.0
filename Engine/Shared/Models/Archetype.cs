using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;

namespace Shared.Models
{
    /// <summary>
    /// Stores entities with the exact same component composition in contiguous memory.
    /// </summary>
    public class Archetype
    {
        private readonly List<int> _entityIds = new();
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
            _entityIds.Add(entityId);
            foreach (var type in Signature)
            {
                _componentArrays[type].Add(components[type]);
            }
        }

        public void RemoveEntity(int entityId)
        {
            int index = _entityIds.IndexOf(entityId);
            if (index == -1) return;

            int lastIndex = _entityIds.Count - 1;
            if (index != lastIndex)
            {
                _entityIds[index] = _entityIds[lastIndex];
                foreach (var array in _componentArrays.Values)
                {
                    array[index] = array[lastIndex];
                }
            }

            _entityIds.RemoveAt(lastIndex);
            foreach (var array in _componentArrays.Values)
            {
                array.RemoveAt(lastIndex);
            }
        }

        public T[] GetComponents<T>() where T : class, IComponent
        {
            if (_componentArrays.TryGetValue(typeof(T), out var list))
            {
                return ((List<T>)list).ToArray();
            }
            return Array.Empty<T>();
        }

        public bool ContainsEntity(int entityId) => _entityIds.Contains(entityId);

        public IComponent? GetComponent(int entityId, Type type)
        {
            int index = _entityIds.IndexOf(entityId);
            if (index != -1 && _componentArrays.TryGetValue(type, out var array))
            {
                return (IComponent)array[index]!;
            }
            return null;
        }

        public IEnumerable<int> GetEntityIds() => _entityIds;
    }
}
