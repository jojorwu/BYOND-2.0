using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;
using Shared.Enums;

namespace Shared.Models;

public readonly struct ArchetypeChunk<T> where T : class, IComponent
{
    public readonly T[] Components;
    public readonly long[] EntityIds;
    public readonly int Offset;
    public readonly int Count;

    public ArchetypeChunk(T[] components, long[] entityIds, int offset, int count)
    {
        Components = components;
        EntityIds = entityIds;
        Offset = offset;
        Count = count;
    }

    public ReadOnlySpan<T> ComponentsSpan => Components.AsSpan(Offset, Count);
    public Span<T> ComponentsMutableSpan => Components.AsSpan(Offset, Count);
    public ReadOnlySpan<long> EntityIdsSpan => EntityIds.AsSpan(Offset, Count);
}

/// <summary>
/// Stores entities with the exact same component composition in contiguous memory.
/// </summary>
public class Archetype
{
    private long[] _entityIds = System.Array.Empty<long>();
    private IGameObject[] _entities = System.Array.Empty<IGameObject>();
    private readonly Dictionary<long, int> _entityIdToIndex = new();
    internal readonly IComponentArray?[] _componentArrays;
    internal readonly ConcurrentDictionary<Type, Archetype> AddTransitions = new();
    internal readonly ConcurrentDictionary<Type, Archetype> RemoveTransitions = new();
    private readonly System.Threading.Lock _lock = new();
    private int _count = 0;
    private int _capacity = 0;
    public ComponentSignature Signature { get; }

    public Archetype(IEnumerable<Type> signature) : this(new ComponentSignature(signature))
    {
    }

    public Archetype(ComponentSignature signature)
    {
        Signature = signature;
        _componentArrays = new IComponentArray[Services.ComponentIdRegistry.Count + 32]; // Buffer for new types
        foreach (var type in signature.Types)
        {
            var arrayType = typeof(ComponentArray<>).MakeGenericType(type);
            int id = Services.ComponentIdRegistry.GetId(type);
            if (id >= _componentArrays.Length) Array.Resize(ref _componentArrays, id + 16);
            _componentArrays[id] = (IComponentArray)Activator.CreateInstance(arrayType)!;
        }
    }

    public int EntityCount => _count;

    private void EnsureCapacity(int required)
    {
        if (required <= _capacity) return;

        _capacity = _capacity == 0 ? 8 : _capacity * 2;
        while (_capacity < required) _capacity *= 2;

        System.Array.Resize(ref _entityIds, _capacity);
        System.Array.Resize(ref _entities, _capacity);
        var arrays = _componentArrays;
        for (int i = 0; i < arrays.Length; i++)
        {
            arrays[i]?.Resize(_capacity);
        }
    }

    public void AddEntity(IGameObject entity, IDictionary<Type, IComponent> components)
    {
        lock (_lock)
        {
            EnsureCapacity(_count + 1);
            int index = _count++;

            _entityIds[index] = entity.Id;
            _entities[index] = entity;
            _entityIdToIndex[entity.Id] = index;

            var signatureTypes = Signature.Types;
            var signatureIds = Signature.ComponentIds;
            for (int i = 0; i < signatureTypes.Length; i++)
            {
                if (components.TryGetValue(signatureTypes[i], out var comp))
                    _componentArrays[signatureIds[i]]!.Set(index, comp);
            }
            entity.Archetype = this;
            entity.ArchetypeIndex = index;
        }
    }

    /// <summary>
    /// Optimized direct transfer between archetypes.
    /// </summary>
    public void AddEntity(IGameObject entity, Archetype? sourceArchetype, int sourceIndex, (Type Type, IComponent Component)? additional = null, Type? ignoreType = null)
    {
        lock (_lock)
        {
            EnsureCapacity(_count + 1);
            int index = _count++;

            _entityIds[index] = entity.Id;
            _entities[index] = entity;
            _entityIdToIndex[entity.Id] = index;

            var targetArrays = _componentArrays;
            var signatureTypes = Signature.Types;
            var signatureIds = Signature.ComponentIds;

            for (int i = 0; i < signatureTypes.Length; i++)
            {
                var type = signatureTypes[i];
                int id = signatureIds[i];

                if (additional.HasValue && additional.Value.Type == type)
                {
                    targetArrays[id]!.Set(index, additional.Value.Component);
                }
                else if (ignoreType != type && sourceArchetype != null)
                {
                    var sourceArray = sourceArchetype.GetComponentsInternal(id);
                    sourceArray?.CopyTo(sourceIndex, targetArrays[id]!, index);
                }
            }

            entity.Archetype = this;
            entity.ArchetypeIndex = index;
        }
    }

    public void RemoveEntity(long entityId)
    {
        lock (_lock)
        {
            if (!_entityIdToIndex.TryGetValue(entityId, out int index)) return;

            IGameObject entity = _entities[index];

            // Only clear properties if the entity currently belongs to this archetype instance.
            // This prevents race conditions where a fast transition has already assigned a new archetype.
            if (entity.Archetype == this)
            {
                entity.Archetype = null;
                entity.ArchetypeIndex = -1;
            }

            int lastIndex = _count - 1;
            var arrays = _componentArrays;

            // Optimization: If the entity is already the last one, we skip the swap and copy
            if (index != lastIndex)
            {
                long lastEntityId = _entityIds[lastIndex];
                IGameObject lastEntity = _entities[lastIndex];

                _entityIds[index] = lastEntityId;
                _entities[index] = lastEntity;
                _entityIdToIndex[lastEntityId] = index;

                for (int i = 0; i < arrays.Length; i++)
                {
                    arrays[i]?.Copy(lastIndex, index);
                }

                lastEntity.ArchetypeIndex = index;
            }

            _entities[lastIndex] = null!;
            _entityIdToIndex.Remove(entityId);
            for (int i = 0; i < arrays.Length; i++)
            {
                arrays[i]?.Clear(lastIndex);
            }
            _count--;
        }
    }

    public ArchetypeChunkEnumerable<T> GetChunks<T>(int chunkSize = 1024) where T : class, IComponent
    {
        int id = Services.ComponentId<T>.Value;
        if (id >= _componentArrays.Length) return default;

        var array = _componentArrays[id];
        if (array == null) return default;

        T[] data;
        long[] entityIds;
        int totalCount;

        lock (_lock)
        {
            data = ((ComponentArray<T>)array).Data;
            entityIds = _entityIds;
            totalCount = _count;
        }

        return new ArchetypeChunkEnumerable<T>(data, entityIds, totalCount, chunkSize);
    }

    public readonly struct ArchetypeChunkEnumerable<T> : IEnumerable<ArchetypeChunk<T>> where T : class, IComponent
    {
        private readonly T[] _data;
        private readonly long[] _entityIds;
        private readonly int _totalCount;
        private readonly int _chunkSize;

        public ArchetypeChunkEnumerable(T[] data, long[] entityIds, int totalCount, int chunkSize)
        {
            _data = data;
            _entityIds = entityIds;
            _totalCount = totalCount;
            _chunkSize = chunkSize;
        }

        public ArchetypeChunkEnumerator<T> GetEnumerator() => new(_data, _entityIds, _totalCount, _chunkSize);
        IEnumerator<ArchetypeChunk<T>> IEnumerable<ArchetypeChunk<T>>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public struct ArchetypeChunkEnumerator<T> : IEnumerator<ArchetypeChunk<T>> where T : class, IComponent
    {
        private readonly T[] _data;
        private readonly long[] _entityIds;
        private readonly int _totalCount;
        private readonly int _chunkSize;
        private int _currentOffset;
        private ArchetypeChunk<T> _current;

        public ArchetypeChunkEnumerator(T[] data, long[] entityIds, int totalCount, int chunkSize)
        {
            _data = data;
            _entityIds = entityIds;
            _totalCount = totalCount;
            _chunkSize = chunkSize;
            _currentOffset = -chunkSize;
            _current = default;
        }

        public bool MoveNext()
        {
            _currentOffset += _chunkSize;
            if (_currentOffset >= _totalCount) return false;

            int count = Math.Min(_chunkSize, _totalCount - _currentOffset);
            _current = new ArchetypeChunk<T>(_data, _entityIds, _currentOffset, count);
            return true;
        }

        public ArchetypeChunk<T> Current => _current;
        object IEnumerator.Current => _current;
        public void Reset() => _currentOffset = -_chunkSize;
        public void Dispose() { }
    }

    internal T[] GetComponentsInternal<T>() where T : class, IComponent
    {
        int id = Services.ComponentId<T>.Value;
        if (id < _componentArrays.Length)
        {
            var array = _componentArrays[id];
            if (array != null) return ((ComponentArray<T>)array).Data;
        }
        return System.Array.Empty<T>();
    }

    internal IComponentArray? GetComponentsInternal(int id)
    {
        return id < _componentArrays.Length ? _componentArrays[id] : null;
    }

    internal IComponentArray? GetComponentsInternal(Type type)
    {
        int id = Services.ComponentIdRegistry.GetId(type);
        return GetComponentsInternal(id);
    }

    /// <summary>
    /// Executes an action on each component of type T. Faster than GetComponents as it avoids array copying.
    /// WARNING: The action must not perform structural changes on this archetype.
    /// </summary>
    public void ForEach<T>(Action<T, long> action) where T : class, IComponent
    {
        int id = Services.ComponentId<T>.Value;
        if (id < _componentArrays.Length)
        {
            var array = _componentArrays[id];
            if (array != null)
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
    }

    public interface IComponentVisitor<T> where T : class, IComponent
    {
        void Visit(T component, long entityId);
    }

    /// <summary>
    /// Executes a visitor on each component of type T.
    /// Eliminates delegate and closure allocations compared to ForEach(Action).
    /// </summary>
    public void ForEach<T, TVisitor>(ref TVisitor visitor) where T : class, IComponent where TVisitor : struct, IComponentVisitor<T>
    {
        int id = Services.ComponentId<T>.Value;
        if (id < _componentArrays.Length)
        {
            var array = _componentArrays[id];
            if (array != null)
            {
                lock (_lock)
                {
                    var data = ((ComponentArray<T>)array).Data;
                    var entityIds = _entityIds;
                    int count = _count;
                    for (int i = 0; i < count; i++)
                    {
                        visitor.Visit(data[i], entityIds[i]);
                    }
                }
            }
        }
    }

    internal void SetComponentInternal(int index, int id, IComponent component)
    {
        if (id < _componentArrays.Length)
        {
            var array = _componentArrays[id];
            if (array != null)
            {
                lock (_lock)
                {
                    array.Set(index, component);
                }
            }
        }
    }

    internal void SetComponentInternal(int index, Type type, IComponent component)
    {
        SetComponentInternal(index, Services.ComponentIdRegistry.GetId(type), component);
    }

    public void ForEachEntity(Action<IGameObject> action)
    {
        lock (_lock)
        {
            var entities = _entities;
            int count = _count;
            for (int i = 0; i < count; i++)
            {
                action(entities[i]);
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

    public int GetComponents(Type type, IComponent[] destination)
    {
        int id = Services.ComponentIdRegistry.GetId(type);
        if (id < _componentArrays.Length)
        {
            var array = _componentArrays[id];
            if (array != null)
            {
                lock (_lock)
                {
                    int count = Math.Min(_count, destination.Length);
                    for (int i = 0; i < count; i++) destination[i] = array.Get(i);
                    return count;
                }
            }
        }
        return 0;
    }

    public IEnumerable<IComponent> GetComponents(Type type)
    {
        int id = Services.ComponentIdRegistry.GetId(type);
        if (id < _componentArrays.Length)
        {
            var array = _componentArrays[id];
            if (array != null)
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
        }
        return Array.Empty<IComponent>();
    }

    public bool ContainsEntity(long entityId)
    {
        lock (_lock)
        {
            return _entityIdToIndex.ContainsKey(entityId);
        }
    }

    public void SetComponent(long entityId, IComponent component)
    {
        lock (_lock)
        {
            if (_entityIdToIndex.TryGetValue(entityId, out int index))
            {
                var type = component.GetType();
                int id = Services.ComponentIdRegistry.GetId(type);
                if (id < _componentArrays.Length)
                {
                    var array = _componentArrays[id];
                    array?.Set(index, component);
                }
            }
        }
    }

    public IComponent? GetComponent(long entityId, Type type)
    {
        lock (_lock)
        {
            if (_entityIdToIndex.TryGetValue(entityId, out int index))
            {
                int id = Services.ComponentIdRegistry.GetId(type);
                if (id < _componentArrays.Length)
                {
                    var array = _componentArrays[id];
                    return array?.Get(index);
                }
            }
        }
        return null;
    }

    public struct ComponentEnumerator : IEnumerator<IComponent>, IEnumerable<IComponent>
    {
        private readonly Archetype _archetype;
        private readonly int _entityIndex;
        private int _arrayIndex;
        private IComponent? _current;

        public ComponentEnumerator(Archetype archetype, int entityIndex)
        {
            _archetype = archetype;
            _entityIndex = entityIndex;
            _arrayIndex = -1;
            _current = null;
        }

        public bool MoveNext()
        {
            var arrays = _archetype._componentArrays;
            while (++_arrayIndex < arrays.Length)
            {
                var array = arrays[_arrayIndex];
                if (array != null)
                {
                    _current = array.Get(_entityIndex);
                    if (_current != null) return true;
                }
            }
            return false;
        }

        public IComponent Current => _current!;
        object IEnumerator.Current => _current!;
        public void Reset() => _arrayIndex = -1;
        public void Dispose() { }
        public IEnumerator<IComponent> GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => this;
    }

    public ComponentEnumerator GetAllComponents(long entityId)
    {
        lock (_lock)
        {
            if (!_entityIdToIndex.TryGetValue(entityId, out int index))
                return default;

            return new ComponentEnumerator(this, index);
        }
    }


    public void Compact()
    {
        lock (_lock)
        {
            if (_capacity > _count * 2 && _capacity > 8)
            {
                _capacity = Math.Max(_count, 8);
                System.Array.Resize(ref _entityIds, _capacity);
                var arrays = _componentArrays;
                for (int i = 0; i < arrays.Length; i++)
                {
                    arrays[i]?.Resize(_capacity);
                }
            }
        }
    }

    internal interface IComponentArray
    {
        void Resize(int capacity);
        void Set(int index, IComponent component);
        void Copy(int from, int to);
        void CopyTo(int sourceIndex, IComponentArray destination, int destinationIndex);
        void Clear(int index);
        IComponent Get(int index);
    }

    private class ComponentArray<T> : IComponentArray where T : class, IComponent
    {
        public T[] Data = System.Array.Empty<T>();

        public void Resize(int capacity) => System.Array.Resize(ref Data, capacity);
        public void Set(int index, IComponent component) => Data[index] = (T)component;
        public void Copy(int from, int to) => Data[to] = Data[from];
        public void CopyTo(int sourceIndex, IComponentArray destination, int destinationIndex)
        {
            destination.Set(destinationIndex, Data[sourceIndex]);
        }
        public void Clear(int index) => Data[index] = null!;
        public IComponent Get(int index) => Data[index];
    }

    public long[] GetEntityIdsSnapshot()
    {
        lock (_lock)
        {
            int count = _count;
            long[] snapshot = new long[count];
            Array.Copy(_entityIds, snapshot, count);
            return snapshot;
        }
    }

    public IGameObject[] GetEntitiesSnapshot()
    {
        lock (_lock)
        {
            int count = _count;
            IGameObject[] snapshot = new IGameObject[count];
            Array.Copy(_entities, snapshot, count);
            return snapshot;
        }
    }

    public void CopyEntitiesTo(IGameObject[] destination, int offset)
    {
        lock (_lock)
        {
            Array.Copy(_entities, 0, destination, offset, _count);
        }
    }

    /// <summary>
    /// Allocation-free entity enumerator for internal use under lock or snapshot.
    /// </summary>
    public struct EntityEnumerator : IEnumerator<IGameObject>
    {
        private readonly IGameObject[] _entities;
        private readonly int _count;
        private int _index;

        public EntityEnumerator(IGameObject[] entities, int count)
        {
            _entities = entities;
            _count = count;
            _index = -1;
        }

        public bool MoveNext() => ++_index < _count;
        public IGameObject Current => _entities[_index];
        object IEnumerator.Current => Current;
        public void Reset() => _index = -1;
        public void Dispose() { }
        public EntityEnumerator GetEnumerator() => this;
    }

    public EntityEnumerator GetEntities()
    {
        // Internal method should only be used when the caller handles synchronization
        // or uses a snapshot (which is already a copy of the array reference).
        return new EntityEnumerator(_entities, _count);
    }
}
