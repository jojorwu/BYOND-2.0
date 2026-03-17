using System.Collections.Concurrent;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Services;
    public class SystemRegistry : ISystemRegistry
    {
        public event Action? SystemsChanged;
        private readonly ConcurrentDictionary<string, ISystem> _systems = new();
        private volatile ISystem[] _allSystems = Array.Empty<ISystem>();

        public void Register(ISystem system)
        {
            _systems[system.Name] = system;
            lock (_systems)
            {
                _allSystems = _systems.Values.ToArray();
            }
            SystemsChanged?.Invoke();
        }

        public void RegisterRange(IEnumerable<ISystem> systems)
        {
            foreach (var system in systems)
            {
                _systems[system.Name] = system;
            }
            lock (_systems)
            {
                _allSystems = _systems.Values.ToArray();
            }
            SystemsChanged?.Invoke();
        }

        public void Unregister(string systemName)
        {
            if (_systems.TryRemove(systemName, out _))
            {
                lock (_systems)
                {
                    _allSystems = _systems.Values.ToArray();
                }
                SystemsChanged?.Invoke();
            }
        }

        public IEnumerable<ISystem> GetSystems()
        {
            return _allSystems;
        }

        public ISystem? GetSystem(string systemName)
        {
            return _systems.TryGetValue(systemName, out var system) ? system : null;
        }
    }
