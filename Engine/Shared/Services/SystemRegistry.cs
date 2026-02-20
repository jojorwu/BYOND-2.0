using System.Collections.Concurrent;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Services;
    public class SystemRegistry : ISystemRegistry
    {
        private readonly ConcurrentDictionary<string, ISystem> _systems = new();

        public void Register(ISystem system)
        {
            _systems[system.Name] = system;
        }

        public void Unregister(string systemName)
        {
            _systems.TryRemove(systemName, out _);
        }

        public IEnumerable<ISystem> GetSystems()
        {
            return _systems.Values;
        }

        public ISystem? GetSystem(string systemName)
        {
            return _systems.TryGetValue(systemName, out var system) ? system : null;
        }
    }
