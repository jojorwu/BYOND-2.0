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
        private int[] _entityIds = System.Array.Empty<int>();
        private readonly Dictionary<int, int> _entityIdToIndex = new();
        private readonly Dictionary<Type, IComponentArray> _componentArrays = new();
        private int _count = 0;
        private int _capacity = 0;
        public HashSet<Type> Signature { get; }

        public Archetype(IEnumerable<Type> signature)
        {
            Signature = new HashSet<Type>(signature);
            foreach (var type in Signature)
            {
                var arrayType = typeof(ComponentArray<>).MakeGenericType(type);
                _componentArrays[type] = (IComponentArray)Activator.CreateInstance(arrayType)!;
            }
        }

        public int EntityCount => _count;

        private void EnsureCapacity(int required)
        {
            if (required <= _capacity) return;

            _capacity = _capacity == 0 ? 8 : _capacity * 2;
            while (_capacity < required) _capacity *= 2;

            System.Array.Resize(ref _entityIds, _capacity);
            foreach (var array in _componentArrays.Values)
            {
                array.Resize(_capacity);
            }
        }

        public void AddEntity(int entityId, IDictionary<Type, IComponent> components)
        {
            EnsureCapacity(_count + 1);
            int index = _count++;

            _entityIds[index] = entityId;
            _entityIdToIndex[entityId] = index;
            foreach (var type in Signature)
            {
                _componentArrays[type].Set(index, components[type]);
            }
        }

        public void RemoveEntity(int entityId)
        {
            if (!_entityIdToIndex.TryGetValue(entityId, out int index)) return;

            int lastIndex = _count - 1;
            if (index != lastIndex)
            {
                int lastEntityId = _entityIds[lastIndex];
                _entityIds[index] = lastEntityId;
                _entityIdToIndex[lastEntityId] = index;

                foreach (var array in _componentArrays.Values)
                {
                    array.Copy(lastIndex, index);
                }
            }

            _entityIdToIndex.Remove(entityId);
            foreach (var array in _componentArrays.Values)
            {
                array.Clear(lastIndex);
            }
            _count--;
        }

        internal T[] GetComponentsInternal<T>() where T : class, IComponent
        {
            if (_componentArrays.TryGetValue(typeof(T), out var array))
            {
                return ((ComponentArray<T>)array).Data;
            }
            return System.Array.Empty<T>();
        }

        internal IComponentArray? GetComponentsInternal(Type type)
        {
            _componentArrays.TryGetValue(type, out var array);
            return array;
        }

        public IEnumerable<T> GetComponents<T>() where T : class, IComponent
        {
            var data = GetComponentsInternal<T>();
            for (int i = 0; i < _count; i++) yield return data[i];
        }

        public IEnumerable<IComponent> GetComponents(Type type)
        {
            if (_componentArrays.TryGetValue(type, out var array))
            {
                for (int i = 0; i < _count; i++) yield return array.Get(i);
            }
        }

        public bool ContainsEntity(int entityId) => _entityIdToIndex.ContainsKey(entityId);

        public IComponent? GetComponent(int entityId, Type type)
        {
            if (_entityIdToIndex.TryGetValue(entityId, out int index) && _componentArrays.TryGetValue(type, out var array))
            {
                return array.Get(index);
            }
            return null;
        }

        public void Compact()
        {
            if (_capacity > _count * 2 && _capacity > 8)
            {
                _capacity = Math.Max(_count, 8);
                System.Array.Resize(ref _entityIds, _capacity);
                foreach (var array in _componentArrays.Values)
                {
                    array.Resize(_capacity);
                }
            }
        }

        internal interface IComponentArray
        {
            void Resize(int capacity);
            void Set(int index, IComponent component);
            void Copy(int from, int to);
            void Clear(int index);
            IComponent Get(int index);
        }

        private class ComponentArray<T> : IComponentArray where T : class, IComponent
        {
            public T[] Data = System.Array.Empty<T>();

            public void Resize(int capacity) => System.Array.Resize(ref Data, capacity);
            public void Set(int index, IComponent component) => Data[index] = (T)component;
            public void Copy(int from, int to) => Data[to] = Data[from];
            public void Clear(int index) => Data[index] = null!;
            public IComponent Get(int index) => Data[index];
        }

        public IEnumerable<int> GetEntityIds() => _entityIds;
    }
}
