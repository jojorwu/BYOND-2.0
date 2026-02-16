using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;

namespace Shared.Services
{
    public class ComponentQueryService : IComponentQueryService
    {
        private readonly IComponentManager _componentManager;
        private readonly ConcurrentDictionary<Type, List<(Action<ComponentEventArgs> Added, Action<ComponentEventArgs> Removed)>> _subscriptions = new();

        public ComponentQueryService(IComponentManager componentManager)
        {
            _componentManager = componentManager;
            _componentManager.ComponentAdded += OnComponentAdded;
            _componentManager.ComponentRemoved += OnComponentRemoved;
        }

        public IEnumerable<IGameObject> Query<T>() where T : class, IComponent
        {
            return _componentManager.GetComponents<T>().Select(c => c.Owner).Where(o => o != null)!;
        }

        public IEnumerable<IGameObject> Query(params Type[] componentTypes)
        {
            if (componentTypes == null || componentTypes.Length == 0)
                return Enumerable.Empty<IGameObject>();

            var smallestSet = componentTypes
                .Select(t => (Type: t, Count: GetCount(t)))
                .OrderBy(x => x.Count)
                .First();

            var candidates = GetOwners(smallestSet.Type);

            foreach (var type in componentTypes.Where(t => t != smallestSet.Type))
            {
                var ownersOfType = new HashSet<int>(GetOwners(type).Select(o => o.Id));
                candidates = candidates.Where(o => ownersOfType.Contains(o.Id));
            }

            return candidates;
        }

        private int GetCount(Type t)
        {
            var results = _componentManager.GetComponents(t);
            int count = 0;
            foreach (var _ in results) count++;
            return count;
        }

        private IEnumerable<IGameObject> GetOwners(Type t)
        {
            var results = _componentManager.GetComponents(t);
            return results.Select(c => c.Owner).Where(o => o != null)!;
        }

        public void Subscribe<T>(Action<ComponentEventArgs> onAdded, Action<ComponentEventArgs> onRemoved) where T : class, IComponent
        {
            var list = _subscriptions.GetOrAdd(typeof(T), _ => new List<(Action<ComponentEventArgs>, Action<ComponentEventArgs>)>());
            lock (list)
            {
                list.Add((onAdded, onRemoved));
            }
        }

        public void Unsubscribe<T>(Action<ComponentEventArgs> onAdded, Action<ComponentEventArgs> onRemoved) where T : class, IComponent
        {
            if (_subscriptions.TryGetValue(typeof(T), out var list))
            {
                lock (list)
                {
                    list.Remove((onAdded, onRemoved));
                }
            }
        }

        private void OnComponentAdded(object? sender, ComponentEventArgs e)
        {
            if (_subscriptions.TryGetValue(e.ComponentType, out var list))
            {
                List<(Action<ComponentEventArgs> Added, Action<ComponentEventArgs> Removed)> copy;
                lock (list) copy = list.ToList();
                foreach (var sub in copy) sub.Added(e);
            }
        }

        private void OnComponentRemoved(object? sender, ComponentEventArgs e)
        {
            if (_subscriptions.TryGetValue(e.ComponentType, out var list))
            {
                List<(Action<ComponentEventArgs> Added, Action<ComponentEventArgs> Removed)> copy;
                lock (list) copy = list.ToList();
                foreach (var sub in copy) sub.Removed(e);
            }
        }
    }
}
