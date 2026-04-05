using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shared.Interfaces;
using Shared.Attributes;

namespace Shared.Services;
[EngineService(typeof(IComponentManager))]
public class ComponentManager : EngineService, IComponentManager, IEngineLifecycle
    {
        private readonly IArchetypeManager _archetypeManager;
        private readonly Dictionary<string, Type> _componentTypesByName = new(StringComparer.OrdinalIgnoreCase);
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

            // Lock the registry to optimize lookup performance
            ComponentIdRegistry.Freeze();

        return Task.CompletedTask;
        }

        private static readonly System.Buffers.SearchValues<string> _assemblyKeywords = System.Buffers.SearchValues.Create(["Shared", "Engine", "Game", "Client", "Server"], StringComparison.Ordinal);

        private static bool IsProbablyComponentAssembly(Assembly assembly)
        {
            var name = assembly.GetName().Name;
            if (name == null) return false;
            return name.AsSpan().ContainsAny(_assemblyKeywords);
        }

        private static readonly ConcurrentDictionary<Type, Func<IComponent>> _factories = new();

        private static Func<IComponent> GetFactory(Type type)
        {
            return _factories.GetOrAdd(type, t =>
            {
                var constructor = t.GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException($"Component {t.Name} must have a parameterless constructor.");
                var body = System.Linq.Expressions.Expression.New(constructor);
                return System.Linq.Expressions.Expression.Lambda<Func<IComponent>>(body).Compile();
            });
        }

        public IComponent? CreateComponent(string componentName)
        {
            if (_componentTypesByName.TryGetValue(componentName, out var type))
            {
                var pool = _componentPools.GetOrAdd(type, t =>
                {
                    var factory = GetFactory(t);
                    var poolType = typeof(SharedPool<>).MakeGenericType(t);
                    return (IObjectPool<IComponent>)Activator.CreateInstance(poolType, factory)!;
                });
                return pool.Rent();
            }
            return null;
        }

        public T CreateComponent<T>() where T : class, IComponent, new()
        {
            var pool = _componentPools.GetOrAdd(typeof(T), t => (IObjectPool<IComponent>)new SharedPool<T>(() => new T()));
            return (T)pool.Rent();
        }

        public void AddComponent<T>(IGameObject owner, T component) where T : class, IComponent
        {
            AddComponent(owner, (IComponent)component);
        }

        public void AddComponent(IGameObject owner, IComponent component)
        {
            _archetypeManager.AddComponent(owner, component);
            ComponentAdded?.Invoke(this, new ComponentEventArgs(owner, component, component.GetType()));
        }

        public void SetDataComponent<T>(IGameObject owner, T component) where T : struct, IDataComponent
        {
            _archetypeManager.SetDataComponent(owner, component);
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

                if (_componentPools.TryGetValue(componentType, out var pool))
                {
                    pool.Return(component);
                }
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
                pool.Shrink();
            }
        }
    }
