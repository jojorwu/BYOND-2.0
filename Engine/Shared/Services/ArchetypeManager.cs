using Shared.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services;

public interface IArchetypeManager
{
    event EventHandler<Archetype>? ArchetypeCreated;
    void AddEntity(IGameObject entity);
    void RemoveEntity(long entityId);
    void AddComponent<T>(IGameObject entity, T component) where T : class, IComponent;
    void AddComponent(IGameObject entity, IComponent component);
    void RemoveComponent<T>(IGameObject entity) where T : class, IComponent;
    void RemoveComponent(IGameObject entity, Type componentType);
    T? GetComponent<T>(long entityId) where T : class, IComponent;
    IComponent? GetComponent(long entityId, Type componentType);
    IEnumerable<T> GetComponents<T>() where T : class, IComponent;
    IEnumerable<ArchetypeChunk<T>> GetChunks<T>() where T : class, IComponent;
    IEnumerable<IComponent> GetComponents(Type componentType);
    IEnumerable<IComponent> GetAllComponents(long entityId);
    IEnumerable<Archetype> GetArchetypesWithComponents(params Type[] componentTypes);
    void ForEach<T>(Action<T, long> action) where T : class, IComponent;
    void ForEachEntity(Action<IGameObject> action);
    void Compact();
}

public class ArchetypeManager : EngineService, IArchetypeManager
{
    public event EventHandler<Archetype>? ArchetypeCreated;
    private volatile Archetype[] _archetypes = Array.Empty<Archetype>();
    private readonly Dictionary<ComponentSignature, Archetype> _signatureToArchetype = new();
    private readonly Dictionary<(Archetype, Type), Archetype> _addTransitionsCache = new();
    private readonly Dictionary<(Archetype, Type), Archetype> _removeTransitionsCache = new();
    private readonly object _archetypeLock = new();
    private readonly ConcurrentDictionary<Type, Archetype[]> _typeToArchetypesCache = new();
    private readonly ConcurrentDictionary<long, Archetype> _entityToArchetype = new();
    private readonly object[] _entityLocks = Enumerable.Range(0, 256).Select(_ => new object()).ToArray();
    private readonly ILogger<ArchetypeManager> _logger;

    public ArchetypeManager(ILogger<ArchetypeManager> logger)
    {
        _logger = logger;
    }

    private object GetEntityLock(long entityId) => _entityLocks[(ulong)entityId % (ulong)_entityLocks.Length];

    public void AddEntity(IGameObject entity)
    {
        lock (GetEntityLock(entity.Id))
        {
            MoveToArchetypeInternal(entity, null, null);
        }
    }

    public void RemoveEntity(long entityId)
    {
        lock (GetEntityLock(entityId))
        {
            if (_entityToArchetype.TryRemove(entityId, out var archetype))
            {
                archetype.RemoveEntity(entityId);
            }
        }
    }

    public void ForEach<T, TVisitor>(ref TVisitor visitor) where T : class, IComponent where TVisitor : struct, Archetype.IComponentVisitor<T>
    {
        if (_typeToArchetypesCache.TryGetValue(typeof(T), out var archetypes))
        {
            foreach (var archetype in archetypes)
            {
                archetype.ForEach<T, TVisitor>(ref visitor);
            }
        }
    }

    public void AddComponent<T>(IGameObject entity, T component) where T : class, IComponent
    {
        AddComponent(entity, (IComponent)component);
    }

    public void AddComponent(IGameObject entity, IComponent component)
    {
        var componentType = component.GetType();
        lock (GetEntityLock(entity.Id))
        {
            _entityToArchetype.TryGetValue(entity.Id, out var currentArchetype);

            component.Owner = entity;
            component.Initialize();

            if (currentArchetype != null)
            {
                if (currentArchetype.Signature.Mask.Get(ComponentIdRegistry.GetId(componentType)))
                {
                    currentArchetype.SetComponentInternal(entity.ArchetypeIndex, componentType, component);
                    return;
                }

                // Fast-path: check transition cache first
                Archetype? targetArchetype;
                lock (_archetypeLock)
                {
                    _addTransitionsCache.TryGetValue((currentArchetype, componentType), out targetArchetype);
                }

                if (targetArchetype != null)
                {
                    int oldIndex = entity.ArchetypeIndex;
                    targetArchetype.AddEntity(entity, currentArchetype, oldIndex, (componentType, component));
                    int newIndex = entity.ArchetypeIndex;
                    currentArchetype.RemoveEntity(entity.Id);
                    entity.Archetype = targetArchetype;
                    entity.ArchetypeIndex = newIndex;
                    _entityToArchetype[entity.Id] = targetArchetype;
                    return;
                }
            }

            MoveToArchetypeInternal(entity, componentType, null, true, component);

            // Populate transition cache
            if (_entityToArchetype.TryGetValue(entity.Id, out var newArchetype) && currentArchetype != null)
            {
                lock (_archetypeLock)
                {
                    _addTransitionsCache[(currentArchetype, componentType)] = newArchetype;
                    _removeTransitionsCache[(newArchetype, componentType)] = currentArchetype;
                }
            }
        }
    }

    private Dictionary<Type, IComponent> GetEntityComponentsInternal(long entityId, Archetype? archetype)
    {
        var dict = new Dictionary<Type, IComponent>();
        if (archetype != null)
        {
            foreach (var type in archetype.Signature.Types)
            {
                var comp = archetype.GetComponent(entityId, type);
                if (comp != null) dict[type] = comp;
            }
        }
        return dict;
    }

    public void ForEachEntity(Action<IGameObject> action)
    {
        var archetypes = _archetypes;
        foreach (var archetype in archetypes)
        {
            archetype.ForEachEntity(action);
        }
    }

    public void RemoveComponent<T>(IGameObject entity) where T : class, IComponent
    {
        RemoveComponent(entity, typeof(T));
    }

    public void RemoveComponent(IGameObject entity, Type componentType)
    {
        lock (GetEntityLock(entity.Id))
        {
            if (_entityToArchetype.TryGetValue(entity.Id, out var currentArchetype) && currentArchetype != null)
            {
                if (currentArchetype.Signature.Mask.Get(ComponentIdRegistry.GetId(componentType)))
                {
                    var component = currentArchetype.GetComponent(entity.Id, componentType);
                    if (component != null)
                    {
                        component.Shutdown();
                        component.Owner = null;

                        Archetype? targetArchetype = null;
                        lock (_archetypeLock)
                        {
                            _removeTransitionsCache.TryGetValue((currentArchetype, componentType), out targetArchetype);
                        }

                        if (targetArchetype != null)
                        {
                            int oldIndex = entity.ArchetypeIndex;
                            targetArchetype.AddEntity(entity, currentArchetype, oldIndex, ignoreType: componentType);
                            int newIndex = entity.ArchetypeIndex;
                            currentArchetype.RemoveEntity(entity.Id);
                            entity.Archetype = targetArchetype;
                            entity.ArchetypeIndex = newIndex;
                            _entityToArchetype[entity.Id] = targetArchetype;
                        }
                        else
                        {
                            var oldArchetype = currentArchetype;
                            MoveToArchetypeInternal(entity, componentType, null, false);
                            // Populate transition cache
                            if (_entityToArchetype.TryGetValue(entity.Id, out var newArchetype) && oldArchetype != null)
                            {
                                lock (_archetypeLock)
                                {
                                    _removeTransitionsCache[(oldArchetype, componentType)] = newArchetype;
                                    _addTransitionsCache[(newArchetype, componentType)] = oldArchetype;
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void MoveToArchetypeInternal(IGameObject entity, Type? componentType, Dictionary<Type, IComponent>? components, bool added = true, IComponent? component = null)
    {
        long entityId = entity.Id;

        _entityToArchetype.TryGetValue(entityId, out var oldArchetype);

        ComponentSignature signature;
        if (oldArchetype != null && componentType != null)
        {
            signature = added ? oldArchetype.Signature.With(componentType) : oldArchetype.Signature.Without(componentType);
        }
        else if (components != null)
        {
            signature = new ComponentSignature(components.Keys);
        }
        else if (componentType != null && added)
        {
            signature = new ComponentSignature(new[] { componentType });
        }
        else
        {
            signature = new ComponentSignature(Array.Empty<Type>());
        }

        // Find or create archetype
        Archetype targetArchetype;
        lock (_archetypeLock)
        {
            if (!_signatureToArchetype.TryGetValue(signature, out targetArchetype!))
            {
                targetArchetype = new Archetype(signature);

                var updatedArchetypes = new Archetype[_archetypes.Length + 1];
                Array.Copy(_archetypes, updatedArchetypes, _archetypes.Length);
                updatedArchetypes[_archetypes.Length] = targetArchetype;
                _archetypes = updatedArchetypes;

                _signatureToArchetype[signature] = targetArchetype;

                ArchetypeCreated?.Invoke(this, targetArchetype);

                foreach (var type in signature.Types)
                {
                    _typeToArchetypesCache.AddOrUpdate(type,
                        _ => new[] { targetArchetype },
                        (_, existing) =>
                        {
                            var updated = new Archetype[existing.Length + 1];
                            System.Array.Copy(existing, updated, existing.Length);
                            updated[existing.Length] = targetArchetype;
                            return updated;
                        });
                }
            }
        }

        if (targetArchetype.Signature.Mask.IsEmpty)
        {
            if (_entityToArchetype.TryRemove(entityId, out var oldArch))
            {
                oldArch.RemoveEntity(entityId);
            }
            return;
        }

        // Add to new FIRST, then remove from old to preserve source data index
        if (oldArchetype != null)
        {
            if (oldArchetype == targetArchetype) return;
            int oldIndex = entity.ArchetypeIndex;

            if (componentType != null)
            {
                if (added && component != null)
                    targetArchetype.AddEntity(entity, oldArchetype, oldIndex, (componentType, component));
                else
                    targetArchetype.AddEntity(entity, oldArchetype, oldIndex, ignoreType: componentType);
            }
            else
            {
                targetArchetype.AddEntity(entity, components ?? (IDictionary<Type, IComponent>)new Dictionary<Type, IComponent>());
            }

            int newIndex = entity.ArchetypeIndex;
            oldArchetype.RemoveEntity(entityId);
            entity.Archetype = targetArchetype;
            entity.ArchetypeIndex = newIndex;
        }
        else
        {
            if (componentType != null && added && component != null)
            {
                var dict = new Dictionary<Type, IComponent> { { componentType, component } };
                targetArchetype.AddEntity(entity, dict);
            }
            else
            {
                targetArchetype.AddEntity(entity, components ?? (IDictionary<Type, IComponent>)new Dictionary<Type, IComponent>());
            }
        }

        _entityToArchetype[entityId] = targetArchetype;
    }

    public T? GetComponent<T>(long entityId) where T : class, IComponent
    {
        return GetComponent(entityId, typeof(T)) as T;
    }

    public IComponent? GetComponent(long entityId, Type componentType)
    {
        if (_entityToArchetype.TryGetValue(entityId, out var archetype))
        {
            return archetype.GetComponent(entityId, componentType);
        }
        return null;
    }

    public IEnumerable<T> GetComponents<T>() where T : class, IComponent
    {
        if (_typeToArchetypesCache.TryGetValue(typeof(T), out var targetArchetypes))
        {
            foreach (var archetype in targetArchetypes)
            {
                foreach (var component in archetype.GetComponents<T>())
                {
                    yield return component;
                }
            }
        }
    }

    public IEnumerable<ArchetypeChunk<T>> GetChunks<T>() where T : class, IComponent
    {
        if (_typeToArchetypesCache.TryGetValue(typeof(T), out var targetArchetypes))
        {
            foreach (var archetype in targetArchetypes)
            {
                yield return archetype.GetChunk<T>();
            }
        }
    }

    public IEnumerable<IComponent> GetComponents(Type componentType)
    {
        if (_typeToArchetypesCache.TryGetValue(componentType, out var targetArchetypes))
        {
            foreach (var archetype in targetArchetypes)
            {
                foreach (var component in archetype.GetComponents(componentType))
                {
                    yield return component;
                }
            }
        }
    }

    public IEnumerable<IComponent> GetAllComponents(long entityId)
    {
        if (_entityToArchetype.TryGetValue(entityId, out var archetype))
        {
            return archetype.GetAllComponents(entityId);
        }
        return Enumerable.Empty<IComponent>();
    }

    public IEnumerable<Archetype> GetArchetypesWithComponents(params Type[] componentTypes)
    {
        if (componentTypes.Length == 0) return Enumerable.Empty<Archetype>();

        var queryMask = new ComponentMask();
        foreach (var type in componentTypes)
        {
            queryMask.Set(ComponentIdRegistry.GetId(type));
        }

        var results = new List<Archetype>();
        var archetypes = _archetypes;
        foreach (var archetype in archetypes)
        {
            if (archetype.Signature.Mask.ContainsAll(queryMask))
            {
                results.Add(archetype);
            }
        }

        return results;
    }

    public void ForEach<T>(Action<T, long> action) where T : class, IComponent
    {
        if (_typeToArchetypesCache.TryGetValue(typeof(T), out var archetypes))
        {
            foreach (var archetype in archetypes)
            {
                archetype.ForEach(action);
            }
        }
    }

    public void Compact()
    {
        var archetypes = _archetypes;
        Parallel.ForEach(archetypes, archetype =>
        {
            archetype.Compact();
        });
    }

    public override Dictionary<string, object> GetDiagnosticInfo()
    {
        var info = base.GetDiagnosticInfo();
        var archetypes = _archetypes;
        info["ArchetypeCount"] = archetypes.Length;
        info["EntityCount"] = _entityToArchetype.Count;
        info["AddTransitionsCacheSize"] = _addTransitionsCache.Count;
        info["RemoveTransitionsCacheSize"] = _removeTransitionsCache.Count;
        return info;
    }
}
