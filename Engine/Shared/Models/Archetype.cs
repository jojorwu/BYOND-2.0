using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;
using Shared.Enums;

namespace Shared.Models;

public struct ArchetypeChunk<T> where T : class, IComponent
{
    public readonly T[] Components;
    public readonly int[] EntityIds;
    public readonly int Count;

    public ArchetypeChunk(T[] components, int[] entityIds, int count)
    {
        Components = components;
        EntityIds = entityIds;
        Count = count;
    }
}

/// <summary>
/// Stores entities with the exact same component composition in contiguous memory.
/// </summary>
public class Archetype
{
    private int[] _entityIds = System.Array.Empty<int>();
    private readonly ConcurrentDictionary<int, int> _entityIdToIndex = new();
    private readonly Dictionary<Type, IComponentArray> _componentArrays = new();
    private readonly object _lock = new();
    private int _count = 0;
    private int _capacity = 0;
    public ComponentSignature Signature { get; }
    public readonly ConcurrentDictionary<Type, Archetype> AddTransitions = new();
    public readonly ConcurrentDictionary<Type, Archetype> RemoveTransitions = new();

    public Archetype(IEnumerable<Type> signature) : this(new ComponentSignature(signature))
    {
    }

    public Archetype(ComponentSignature signature)
    {
        Signature = signature;
        foreach (var type in signature.Types)
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
        lock (_lock)
        {
            EnsureCapacity(_count + 1);
            int index = _count++;

            _entityIds[index] = entityId;
            _entityIdToIndex[entityId] = index;
            foreach (var type in Signature.Types)
            {
                _componentArrays[type].Set(index, components[type]);
            }
        }
    }

    public void RemoveEntity(int entityId)
    {
        lock (_lock)
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

            _entityIdToIndex.TryRemove(entityId, out _);
            foreach (var array in _componentArrays.Values)
            {
                array.Clear(lastIndex);
            }
            _count--;
        }
    }

    internal ArchetypeChunk<T> GetChunk<T>() where T : class, IComponent
    {
        if (_componentArrays.TryGetValue(typeof(T), out var array))
        {
            lock (_lock)
            {
                return new ArchetypeChunk<T>(((ComponentArray<T>)array).Data, _entityIds, _count);
            }
        }
        return new ArchetypeChunk<T>(System.Array.Empty<T>(), System.Array.Empty<int>(), 0);
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

    /// <summary>
    /// Executes an action on each component of type T. Faster than GetComponents as it avoids array copying.
    /// WARNING: The action must not perform structural changes on this archetype.
    /// </summary>
    public void ForEach<T>(Action<T, int> action) where T : class, IComponent
    {
        if (_componentArrays.TryGetValue(typeof(T), out var array))
        {
            lock (_lock)
            {
                var data = ((ComponentArray<T>)array).Data;
                var entityIds = _entityIds;
                int count = _count;
                for (int i = 0; i < count; i++)
                {
                    action(data[i], entityIds[i]);
                }
            }
        }
    }

    public IEnumerable<T> GetComponents<T>() where T : class, IComponent
    {
        T[] data;
        int count;
        lock (_lock)
        {
            var originalData = GetComponentsInternal<T>();
            count = _count;
            // We must copy the array because elements might be swapped or cleared during iteration
            data = new T[count];
            System.Array.Copy(originalData, data, count);
        }
        return data;
    }

    public IEnumerable<IComponent> GetComponents(Type type)
    {
        if (_componentArrays.TryGetValue(type, out var array))
        {
            IComponent[] data;
            lock (_lock)
            {
                int count = _count;
                data = new IComponent[count];
                for (int i = 0; i < count; i++) data[i] = array.Get(i);
            }
            return data;
        }
        return Array.Empty<IComponent>();
    }

    public bool ContainsEntity(int entityId) => _entityIdToIndex.ContainsKey(entityId);

    public IComponent? GetComponent(int entityId, Type type)
    {
        lock (_lock)
        {
            if (_entityIdToIndex.TryGetValue(entityId, out int index) && _componentArrays.TryGetValue(type, out var array))
            {
                return array.Get(index);
            }
        }
        return null;
    }

    public void Compact()
    {
        lock (_lock)
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

    public int[] GetEntityIdsSnapshot()
    {
        lock (_lock)
        {
            int count = _count;
            int[] snapshot = new int[count];
            Array.Copy(_entityIds, snapshot, count);
            return snapshot;
        }
    }
}
