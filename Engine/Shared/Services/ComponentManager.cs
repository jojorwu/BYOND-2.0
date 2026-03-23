using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shared.Interfaces;

namespace Shared.Services;
public class ComponentManager : EngineService, IComponentManager, IEngineLifecycle, IFreezable
    {
        private readonly IArchetypeManager _archetypeManager;
        private readonly Dictionary<string, Type> _componentTypesByName = new(StringComparer.OrdinalIgnoreCase);
        private volatile FrozenDictionary<string, Type> _frozenComponentTypesByName = FrozenDictionary<string, Type>.Empty;
        private readonly ConcurrentDictionary<Type, IObjectPool<IComponent>> _componentPools = new();

        public IArchetypeManager ArchetypeManager => _archetypeManager;

        public event EventHandler<ComponentEventArgs>? ComponentAdded;
        public event EventHandler<ComponentEventArgs>? ComponentRemoved;

        public ComponentManager(IArchetypeManager archetypeManager)
        {
            _archetypeManager = archetypeManager;
    }

    public Task PostInitializeAsync(CancellationToken cancellationToken)
    {
            // Use the Registry for stable ID assignment and discovery
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // We only scan assemblies that might contain components to avoid overhead
                if (IsProbablyComponentAssembly(assembly))
                {
                    ComponentIdRegistry.RegisterAll(assembly);
                }
            }

            // Fill name cache from the registry's discovered types
            foreach (var type in ComponentIdRegistry.RegisteredTypes)
            {
                _componentTypesByName[type.Name] = type;
            }

        return Task.CompletedTask;
        }

        private static readonly System.Buffers.SearchValues<string> _assemblyKeywords = System.Buffers.SearchValues.Create(["Shared", "Engine", "Game", "Client", "Server"], StringComparison.Ordinal);

        private static bool IsProbablyComponentAssembly(Assembly assembly)
        {
            var name = assembly.GetName().Name;
            if (name == null) return false;
            return name.AsSpan().ContainsAny(_assemblyKeywords);
        }

        public void Freeze()
        {
            _frozenComponentTypesByName = _componentTypesByName.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }

        public IComponent? CreateComponent(string componentName)
        {
            if (_frozenComponentTypesByName.TryGetValue(componentName, out var type))
            {
                return RentComponent(type);
            }
            if (_componentTypesByName.TryGetValue(componentName, out type))
            {
                return RentComponent(type);
            }
            return null;
        }

        private IComponent RentComponent(Type type)
        {
            var pool = _componentPools.GetOrAdd(type, t =>
            {
                return new SharedPool<IComponent>(() => (IComponent)Activator.CreateInstance(t)!);
            });
            return pool.Rent();
        }

        private void ReturnComponent(IComponent component)
        {
            var type = component.GetType();
            if (_componentPools.TryGetValue(type, out var pool))
            {
                pool.Return(component);
            }
        }

        public void AddComponent<T>(IGameObject owner, T component) where T : class, IComponent
        {
            AddComponent(owner, (IComponent)component);
        }

        public void AddComponent(IGameObject owner, IComponent component)
        {
            component.Owner = owner;
            component.Initialize();
            _archetypeManager.AddComponent(owner, component);
            ComponentAdded?.Invoke(this, new ComponentEventArgs(owner, component, component.GetType()));
        }

        public void RemoveComponent<T>(IGameObject owner) where T : class, IComponent
        {
            RemoveComponent(owner, typeof(T));
        }

        public void RemoveComponent(IGameObject owner, Type componentType)
        {
            var component = _archetypeManager.GetComponent(owner.Id, componentType);
            if (component != null)
            {
                _archetypeManager.RemoveComponent(owner, componentType);
                ComponentRemoved?.Invoke(this, new ComponentEventArgs(owner, component, componentType));
                ReturnComponent(component);
            }
        }

        public T? GetComponent<T>(IGameObject owner) where T : class, IComponent
        {
            return _archetypeManager.GetComponent<T>(owner.Id);
        }

        public IComponent? GetComponent(IGameObject owner, Type componentType)
        {
            return _archetypeManager.GetComponent(owner.Id, componentType);
        }

        public IEnumerable<T> GetComponents<T>() where T : class, IComponent
        {
            return _archetypeManager.GetComponents<T>();
        }

        public IEnumerable<Models.ArchetypeChunk<T>> GetChunks<T>(int chunkSize = 1024) where T : class, IComponent
        {
            return _archetypeManager.GetChunks<T>(chunkSize);
        }

        public IEnumerable<IComponent> GetComponents(Type componentType)
        {
            return _archetypeManager.GetComponents(componentType);
        }

        public IEnumerable<IComponent> GetAllComponents(IGameObject owner)
        {
            return _archetypeManager.GetAllComponents(owner.Id);
        }

        public void Compact()
        {
            _archetypeManager.Compact();
        }

        public void Shrink()
        {
            Compact();
            foreach (var pool in _componentPools.Values)
            {
                if (pool is IShrinkable shrinkable)
                {
                    shrinkable.Shrink();
                }
            }
        }
    }
