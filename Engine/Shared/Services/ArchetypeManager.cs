using Shared.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    IEnumerable<ArchetypeChunk<T>> GetChunks<T>(int chunkSize = 1024) where T : class, IComponent;
    IEnumerable<IComponent> GetComponents(Type componentType);
    IEnumerable<IComponent> GetAllComponents(long entityId);
    IEnumerable<Archetype> GetArchetypesWithComponents(params ReadOnlySpan<Type> componentTypes);
    void ForEach<T>(Action<T, long> action) where T : class, IComponent;
    void ForEachEntity(Action<IGameObject> action);
    void Compact();
}

public class ArchetypeManager : EngineService, IArchetypeManager, IShrinkable
{
    public event EventHandler<Archetype>? ArchetypeCreated;
    private volatile Archetype[] _archetypes = Array.Empty<Archetype>();
    private readonly Dictionary<ComponentSignature, Archetype> _signatureToArchetype = new();
    private readonly System.Threading.Lock _archetypeLock = new();
    private readonly ConcurrentDictionary<Type, Archetype[]> _typeToArchetypesCache = new();
    private readonly ConcurrentDictionary<long, Archetype> _entityToArchetype = new();
    private readonly System.Threading.Lock[] _entityLocks = Enumerable.Range(0, 1024).Select(_ => new System.Threading.Lock()).ToArray();
    private readonly ILogger<ArchetypeManager> _logger;
    private readonly IDiagnosticBus _diagnosticBus;

    public ArchetypeManager(ILogger<ArchetypeManager> logger, IDiagnosticBus diagnosticBus)
    {
        _logger = logger;
        _diagnosticBus = diagnosticBus;
    }

    private System.Threading.Lock GetEntityLock(long entityId) => _entityLocks[(ulong)entityId % (ulong)_entityLocks.Length];

    public void AddEntity(IGameObject entity)
    {
        using (GetEntityLock(entity.Id).EnterScope())
        {
            MoveToArchetypeInternal(entity, null, null);
        }
    }

    public void RemoveEntity(long entityId)
    {
        using (GetEntityLock(entityId).EnterScope())
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
        using (GetEntityLock(entity.Id).EnterScope())
        {
            _entityToArchetype.TryGetValue(entity.Id, out var currentArchetype);

            component.Owner = entity;
            component.Initialize();

            if (currentArchetype != null)
            {
                int id = ComponentIdRegistry.GetId(componentType);
                if (currentArchetype.Signature.Mask.Get(id))
                {
                    currentArchetype.SetComponentInternal(entity.ArchetypeIndex, id, component);
                    return;
                }

                // Fast-path: check lock-free archetype transitions
                if (currentArchetype.AddTransitions.TryGetValue(componentType, out var targetArchetype))
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

            // Populate lock-free transition map
            if (_entityToArchetype.TryGetValue(entity.Id, out var newArchetype) && currentArchetype != null)
            {
                currentArchetype.AddTransitions.TryAdd(componentType, newArchetype);
                newArchetype.RemoveTransitions.TryAdd(componentType, currentArchetype);
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
        using (GetEntityLock(entity.Id).EnterScope())
        {
            if (_entityToArchetype.TryGetValue(entity.Id, out var currentArchetype) && currentArchetype != null)
            {
                int id = ComponentIdRegistry.GetId(componentType);
                if (currentArchetype.Signature.Mask.Get(id))
                {
                    var component = currentArchetype.GetComponent(entity.Id, componentType);
                    if (component != null)
                    {
                        component.Shutdown();
                        component.Owner = null;

                        // Fast-path: check lock-free archetype transitions
                        if (currentArchetype.RemoveTransitions.TryGetValue(componentType, out var targetArchetype))
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
                            // Populate lock-free transition map
                            if (_entityToArchetype.TryGetValue(entity.Id, out var newArchetype) && oldArchetype != null)
                            {
                                oldArchetype.RemoveTransitions.TryAdd(componentType, newArchetype);
                                newArchetype.AddTransitions.TryAdd(componentType, oldArchetype);
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
        using (_archetypeLock.EnterScope())
        {
            if (!_signatureToArchetype.TryGetValue(signature, out targetArchetype!))
            {
                targetArchetype = new Archetype(signature);

                var updatedArchetypes = new Archetype[_archetypes.Length + 1];
                Array.Copy(_archetypes, updatedArchetypes, _archetypes.Length);
                updatedArchetypes[_archetypes.Length] = targetArchetype;
                _archetypes = updatedArchetypes;

                _signatureToArchetype[signature] = targetArchetype;

                _diagnosticBus.Publish("ArchetypeManager", "New archetype created", DiagnosticSeverity.Info, m => {
                    m.Add("Signature", signature.ToString() ?? string.Empty);
                    m.Add("ArchetypeCount", _archetypes.Length);
                });

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

    public IEnumerable<ArchetypeChunk<T>> GetChunks<T>(int chunkSize = 1024) where T : class, IComponent
    {
        if (_typeToArchetypesCache.TryGetValue(typeof(T), out var targetArchetypes))
        {
            foreach (var archetype in targetArchetypes)
            {
                foreach (var chunk in archetype.GetChunks<T>(chunkSize))
                {
                    yield return chunk;
                }
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

    public IEnumerable<Archetype> GetArchetypesWithComponents(params ReadOnlySpan<Type> componentTypes)
    {
        if (componentTypes.IsEmpty) return Enumerable.Empty<Archetype>();

        // Heuristic: start with the rarest component type to minimize intersection overhead
        Type? rarestType = null;
        int minCount = int.MaxValue;

        foreach (var type in componentTypes)
        {
            if (_typeToArchetypesCache.TryGetValue(type, out var archetypes))
            {
                if (archetypes.Length < minCount)
                {
                    minCount = archetypes.Length;
                    rarestType = type;
                }
            }
            else
            {
                // If any component type has NO archetypes, the query result is empty
                return Enumerable.Empty<Archetype>();
            }
        }

        if (rarestType == null) return Enumerable.Empty<Archetype>();

        var queryMask = new ComponentMask();
        for (int i = 0; i < componentTypes.Length; i++)
        {
            queryMask.Set(ComponentIdRegistry.GetId(componentTypes[i]));
        }

        var candidates = _typeToArchetypesCache[rarestType];
        var results = new List<Archetype>();

        for (int i = 0; i < candidates.Length; i++)
        {
            var archetype = candidates[i];
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
                archetype.ForEach<T>(action);
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

        _diagnosticBus.Publish("ArchetypeManager", "Archetypes compacted", DiagnosticSeverity.Info, m => {
            m.Add("ArchetypeCount", archetypes.Length);
        });
    }

    public void Shrink()
    {
        Compact();
        _typeToArchetypesCache.Clear();
    }

    public override Dictionary<string, object> GetDiagnosticInfo()
    {
        var info = base.GetDiagnosticInfo();
        var archetypes = _archetypes;
        info["ArchetypeCount"] = archetypes.Length;
        info["EntityCount"] = _entityToArchetype.Count;

        long totalMemory = 0;
        int maxEntities = 0;
        foreach (var arch in archetypes) {
            totalMemory += arch.EstimateMemoryUsage();
            maxEntities = Math.Max(maxEntities, arch.EntityCount);
        }

        info["EstimatedMemoryUsage"] = totalMemory;
        info["MaxEntitiesInArchetype"] = maxEntities;

        return info;
    }
}
