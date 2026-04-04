using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Shared.Interfaces;
using Shared.Enums;

namespace Shared.Models;

public readonly struct ArchetypeChunk<T> where T : class, IComponent
{
    public readonly T[] Components;
    public readonly long[] EntityIds;
    public readonly IGameObject[] Entities;
    public readonly long[] Xs;
    public readonly long[] Ys;
    public readonly long[] Zs;
    public readonly int Offset;
    public readonly int Count;

    public ArchetypeChunk(T[] components, long[] entityIds, IGameObject[] entities, long[] xs, long[] ys, long[] zs, int offset, int count)
    {
        Components = components;
        EntityIds = entityIds;
        Entities = entities;
        Xs = xs;
        Ys = ys;
        Zs = zs;
        Offset = offset;
        Count = count;
    }

    public ReadOnlySpan<T> ComponentsSpan => Components.AsSpan(Offset, Count);
    public Span<T> ComponentsMutableSpan => Components.AsSpan(Offset, Count);
    public ReadOnlySpan<long> EntityIdsSpan => EntityIds.AsSpan(Offset, Count);
    public ReadOnlySpan<IGameObject> EntitiesSpan => Entities.AsSpan(Offset, Count);
    public ReadOnlySpan<long> XsSpan => Xs.AsSpan(Offset, Count);
    public ReadOnlySpan<long> YsSpan => Ys.AsSpan(Offset, Count);
    public ReadOnlySpan<long> ZsSpan => Zs.AsSpan(Offset, Count);
}

/// <summary>
/// Stores entities with the exact same component composition in contiguous memory.
/// </summary>
public class Archetype
{
    private long[] _entityIds = System.Array.Empty<long>();
    private IGameObject[] _entities = System.Array.Empty<IGameObject>();
    private long[] _xs = Array.Empty<long>();
    private long[] _ys = Array.Empty<long>();
    private long[] _zs = Array.Empty<long>();
    private readonly Dictionary<long, int> _entityIdToIndex = new();
    internal readonly IComponentArray?[] _componentArrays;
    private IComponentArray[] _activeArrays = Array.Empty<IComponentArray>();
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
        var active = new List<IComponentArray>();
        foreach (var type in signature.Types)
        {
            var arrayType = typeof(ComponentArray<>).MakeGenericType(type);
            int id = Services.ComponentIdRegistry.GetId(type);
            if (id >= _componentArrays.Length) Array.Resize(ref _componentArrays, id + 16);
            var array = (IComponentArray)Activator.CreateInstance(arrayType)!;
            _componentArrays[id] = array;
            active.Add(array);
        }
        _activeArrays = active.ToArray();
    }

    public int EntityCount => _count;

    private void EnsureCapacity(int required)
    {
        if (required <= _capacity) return;

        _capacity = _capacity == 0 ? 8 : _capacity * 2;
        while (_capacity < required) _capacity *= 2;

        System.Array.Resize(ref _entityIds, _capacity);
        System.Array.Resize(ref _entities, _capacity);
        Array.Resize(ref _xs, _capacity);
        Array.Resize(ref _ys, _capacity);
        Array.Resize(ref _zs, _capacity);

        // Pre-size dictionary to avoid rehashing during bursts of additions
        _entityIdToIndex.EnsureCapacity(_capacity);

        var arrays = _activeArrays;
        for (int i = 0; i < arrays.Length; i++)
        {
            arrays[i].Resize(_capacity);
        }
    }

    public void AddEntity(IGameObject entity, IDictionary<Type, IComponent> components)
    {
        using (_lock.EnterScope())
        {
            EnsureCapacity(_count + 1);
            int index = _count++;

            ref long entityId = ref MemoryMarshal.GetArrayDataReference(_entityIds);
            Unsafe.Add(ref entityId, index) = entity.Id;

            ref IGameObject entitiesRef = ref MemoryMarshal.GetArrayDataReference(_entities);
            Unsafe.Add(ref entitiesRef, index) = entity;

            _xs[index] = entity.X;
            _ys[index] = entity.Y;
            _zs[index] = entity.Z;

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
        using (_lock.EnterScope())
        {
            EnsureCapacity(_count + 1);
            int index = _count++;

            ref long entityId = ref MemoryMarshal.GetArrayDataReference(_entityIds);
            Unsafe.Add(ref entityId, index) = entity.Id;

            ref IGameObject entitiesRef = ref MemoryMarshal.GetArrayDataReference(_entities);
            Unsafe.Add(ref entitiesRef, index) = entity;

            _xs[index] = entity.X;
            _ys[index] = entity.Y;
            _zs[index] = entity.Z;

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
        using (_lock.EnterScope())
        {
            if (!_entityIdToIndex.TryGetValue(entityId, out int index)) return;

            ref IGameObject entitiesRef = ref MemoryMarshal.GetArrayDataReference(_entities);
            IGameObject entity = Unsafe.Add(ref entitiesRef, index);

            // Only clear properties if the entity currently belongs to this archetype instance.
            // This prevents race conditions where a fast transition has already assigned a new archetype.
            if (entity.Archetype == this)
            {
                entity.Archetype = null;
                entity.ArchetypeIndex = -1;
            }

            int lastIndex = _count - 1;
            var arrays = _activeArrays;

            // Optimization: If the entity is already the last one, we skip the swap and copy
            if (index != lastIndex)
            {
                ref long entityIdsRef = ref MemoryMarshal.GetArrayDataReference(_entityIds);
                long lastEntityId = Unsafe.Add(ref entityIdsRef, lastIndex);
                IGameObject lastEntity = Unsafe.Add(ref entitiesRef, lastIndex);

                Unsafe.Add(ref entityIdsRef, index) = lastEntityId;
                Unsafe.Add(ref entitiesRef, index) = lastEntity;
                _xs[index] = _xs[lastIndex];
                _ys[index] = _ys[lastIndex];
                _zs[index] = _zs[lastIndex];

                _entityIdToIndex[lastEntityId] = index;

                for (int i = 0; i < arrays.Length; i++)
                {
                    arrays[i].Copy(lastIndex, index);
                }

                lastEntity.ArchetypeIndex = index;
            }

            Unsafe.Add(ref entitiesRef, lastIndex) = null!;
            _entities[lastIndex] = null!; // Double-ensure for safety
            _entityIdToIndex.Remove(entityId);
            for (int i = 0; i < arrays.Length; i++)
            {
                arrays[i].Clear(lastIndex);
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
        IGameObject[] entities;
        long[] xs, ys, zs;
        int totalCount;

        using (_lock.EnterScope())
        {
            data = ((ComponentArray<T>)array).Data;
            entityIds = _entityIds;
            entities = _entities;
            xs = _xs;
            ys = _ys;
            zs = _zs;
            totalCount = _count;
        }

        return new ArchetypeChunkEnumerable<T>(data, entityIds, entities, xs, ys, zs, totalCount, chunkSize);
    }

    public readonly struct ArchetypeChunkEnumerable<T> : IEnumerable<ArchetypeChunk<T>> where T : class, IComponent
    {
        private readonly T[] _data;
        private readonly long[] _entityIds;
        private readonly IGameObject[] _entities;
        private readonly long[] _xs;
        private readonly long[] _ys;
        private readonly long[] _zs;
        private readonly int _totalCount;
        private readonly int _chunkSize;

        public ArchetypeChunkEnumerable(T[] data, long[] entityIds, IGameObject[] entities, long[] xs, long[] ys, long[] zs, int totalCount, int chunkSize)
        {
            _data = data;
            _entityIds = entityIds;
            _entities = entities;
            _xs = xs;
            _ys = ys;
            _zs = zs;
            _totalCount = totalCount;
            _chunkSize = chunkSize;
        }

        public ArchetypeChunkEnumerator<T> GetEnumerator() => new(_data, _entityIds, _entities, _xs, _ys, _zs, _totalCount, _chunkSize);
        IEnumerator<ArchetypeChunk<T>> IEnumerable<ArchetypeChunk<T>>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public struct ArchetypeChunkEnumerator<T> : IEnumerator<ArchetypeChunk<T>> where T : class, IComponent
    {
        private readonly T[] _data;
        private readonly long[] _entityIds;
        private readonly IGameObject[] _entities;
        private readonly long[] _xs;
        private readonly long[] _ys;
        private readonly long[] _zs;
        private readonly int _totalCount;
        private readonly int _chunkSize;
        private int _currentOffset;
        private ArchetypeChunk<T> _current;

        public ArchetypeChunkEnumerator(T[] data, long[] entityIds, IGameObject[] entities, long[] xs, long[] ys, long[] zs, int totalCount, int chunkSize)
        {
            _data = data;
            _entityIds = entityIds;
            _entities = entities;
            _xs = xs;
            _ys = ys;
            _zs = zs;
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
            _current = new ArchetypeChunk<T>(_data, _entityIds, _entities, _xs, _ys, _zs, _currentOffset, count);
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
                using (_lock.EnterScope())
                {
                    var typed = (ComponentArray<T>)array;
                    var entityIds = _entityIds;
                    int count = _count;

                    ref long idRef = ref MemoryMarshal.GetArrayDataReference(entityIds);
                    for (int i = 0; i < count; i++)
                    {
                        action(typed.GetAsT(i), Unsafe.Add(ref idRef, i));
                    }
                }
            }
        }
    }

    public void BeginUpdate()
    {
        using (_lock.EnterScope())
        {
            var arrays = _activeArrays;
            for (int i = 0; i < arrays.Length; i++)
            {
                arrays[i].BeginUpdate(_count);
            }
        }
    }

    public void CommitUpdate()
    {
        using (_lock.EnterScope())
        {
            var arrays = _activeArrays;
            for (int i = 0; i < arrays.Length; i++)
            {
                arrays[i].CommitUpdate(_count);
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
                using (_lock.EnterScope())
                {
                    var data = ((ComponentArray<T>)array).Data;
                    var entityIds = _entityIds;
                    int count = _count;

                    ref T dataRef = ref MemoryMarshal.GetArrayDataReference(data);
                    ref long idRef = ref MemoryMarshal.GetArrayDataReference(entityIds);

                    for (int i = 0; i < count; i++)
                    {
                        visitor.Visit(Unsafe.Add(ref dataRef, i), Unsafe.Add(ref idRef, i));
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
                using (_lock.EnterScope())
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

    public interface IEntityVisitor
    {
        void Visit(IGameObject entity);
    }

    public void ForEachEntity(Action<IGameObject> action)
    {
        using (_lock.EnterScope())
        {
            var entities = _entities;
            int count = _count;
            for (int i = 0; i < count; i++)
            {
                action(entities[i]);
            }
        }
    }

    /// <summary>
    /// Executes a visitor on each entity in the archetype.
    /// Eliminates delegate and closure allocations.
    /// </summary>
    public void ForEachEntity<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IEntityVisitor, allows ref struct
    {
        using (_lock.EnterScope())
        {
            var entities = _entities;
            int count = _count;
            for (int i = 0; i < count; i++)
            {
                visitor.Visit(entities[i]);
            }
        }
    }

    public IEnumerable<T> GetComponents<T>() where T : class, IComponent
    {
        T[] data;
        int count;
        using (_lock.EnterScope())
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
                using (_lock.EnterScope())
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
                using (_lock.EnterScope())
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
        using (_lock.EnterScope())
        {
            return _entityIdToIndex.ContainsKey(entityId);
        }
    }

    public void SetComponent(long entityId, IComponent component)
    {
        using (_lock.EnterScope())
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
        using (_lock.EnterScope())
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
            var arrays = _archetype._activeArrays;
            while (++_arrayIndex < arrays.Length)
            {
                _current = arrays[_arrayIndex].Get(_entityIndex);
                if (_current != null) return true;
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
        using (_lock.EnterScope())
        {
            if (!_entityIdToIndex.TryGetValue(entityId, out int index))
                return default;

            return new ComponentEnumerator(this, index);
        }
    }


    public void SetX(int index, long x) => _xs[index] = x;
    public void SetY(int index, long y) => _ys[index] = y;
    public void SetZ(int index, long z) => _zs[index] = z;

    public void Compact()
    {
        using (_lock.EnterScope())
        {
            if (_capacity > _count * 2 && _capacity > 8)
            {
                _capacity = Math.Max(_count, 8);
                System.Array.Resize(ref _entityIds, _capacity);
                Array.Resize(ref _entities, _capacity);
                Array.Resize(ref _xs, _capacity);
                Array.Resize(ref _ys, _capacity);
                Array.Resize(ref _zs, _capacity);

                var arrays = _activeArrays;
                for (int i = 0; i < arrays.Length; i++)
                {
                    arrays[i].Resize(_capacity);
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
        void BeginUpdate(int count);
        void CommitUpdate(int count);
    }

    private class ComponentArray<T> : IComponentArray where T : class, IComponent
    {
        public T[] Data = System.Array.Empty<T>();
        public T[] NextData = System.Array.Empty<T>();
        private bool _isUpdating = false;

        public void Resize(int capacity)
        {
            System.Array.Resize(ref Data, capacity);
            System.Array.Resize(ref NextData, capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, IComponent component)
        {
            ref T dataRef = ref MemoryMarshal.GetArrayDataReference(Data);
            Unsafe.Add(ref dataRef, index) = (T)component;

            ref T nextRef = ref MemoryMarshal.GetArrayDataReference(NextData);
            Unsafe.Add(ref nextRef, index) = (T)component;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Copy(int from, int to)
        {
            ref T dataRef = ref MemoryMarshal.GetArrayDataReference(Data);
            Unsafe.Add(ref dataRef, to) = Unsafe.Add(ref dataRef, from);

            ref T nextRef = ref MemoryMarshal.GetArrayDataReference(NextData);
            Unsafe.Add(ref nextRef, to) = Unsafe.Add(ref nextRef, from);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(int sourceIndex, IComponentArray destination, int destinationIndex)
        {
            ref T dataRef = ref MemoryMarshal.GetArrayDataReference(Data);
            destination.Set(destinationIndex, Unsafe.Add(ref dataRef, sourceIndex));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(int index)
        {
            ref T dataRef = ref MemoryMarshal.GetArrayDataReference(Data);
            Unsafe.Add(ref dataRef, index) = null!;

            ref T nextRef = ref MemoryMarshal.GetArrayDataReference(NextData);
            Unsafe.Add(ref nextRef, index) = null!;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IComponent Get(int index)
        {
            ref T dataRef = ref MemoryMarshal.GetArrayDataReference(Data);
            return Unsafe.Add(ref dataRef, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetAsT(int index)
        {
            ref T dataRef = ref MemoryMarshal.GetArrayDataReference(_isUpdating ? NextData : Data);
            return Unsafe.Add(ref dataRef, index);
        }

        public void BeginUpdate(int count)
        {
            _isUpdating = true;
            System.Array.Copy(Data, NextData, count);
            for (int i = 0; i < count; i++) NextData[i]?.BeginUpdate();
        }

        public void CommitUpdate(int count)
        {
            _isUpdating = false;
            for (int i = 0; i < count; i++) NextData[i]?.CommitUpdate();
            (Data, NextData) = (NextData, Data);
            // After swap, we also need to update the new NextData to match the new Data
            // to ensure future incremental updates starting from BeginUpdate are correct.
            System.Array.Copy(Data, NextData, count);
        }
    }

    public long[] GetEntityIdsSnapshot()
    {
        using (_lock.EnterScope())
        {
            int count = _count;
            long[] snapshot = new long[count];
            Array.Copy(_entityIds, snapshot, count);
            return snapshot;
        }
    }

    /// <summary>
    /// Rents a snapshot of the current entities. Caller MUST return it to ArrayPool.
    /// </summary>
    public IGameObject[] GetEntitiesSnapshot(out int count)
    {
        using (_lock.EnterScope())
        {
            count = _count;
            if (count == 0) return Array.Empty<IGameObject>();

            IGameObject[] snapshot = ArrayPool<IGameObject>.Shared.Rent(count);
            Array.Copy(_entities, snapshot, count);
            return snapshot;
        }
    }

    public void CopyEntitiesTo(IGameObject[] destination, int offset)
    {
        using (_lock.EnterScope())
        {
            Array.Copy(_entities, 0, destination, offset, _count);
        }
    }

    public long EstimateMemoryUsage()
    {
        using (_lock.EnterScope())
        {
            long usage = _entityIds.Length * sizeof(long);
            usage += _entities.Length * Unsafe.SizeOf<IntPtr>(); // Approximate object references

            // Dictionary overhead (approximate: entries * (size of long + int + next pointer + padding))
            usage += _entityIdToIndex.Count * (sizeof(long) + sizeof(int) + sizeof(int) + 8);

            var arrays = _activeArrays;
            for (int i = 0; i < arrays.Length; i++)
            {
                usage += _capacity * Unsafe.SizeOf<IntPtr>(); // Approximate component references
            }
            return usage;
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
