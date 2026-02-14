using System;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;

namespace Shared.Services
{
    public interface IComponentQueryService
    {
        IEnumerable<IGameObject> Query<T>() where T : class, IComponent;
        IEnumerable<IGameObject> Query(params Type[] componentTypes);
    }

    public class ComponentQueryService : IComponentQueryService
    {
        private readonly IComponentManager _componentManager;

        public ComponentQueryService(IComponentManager componentManager)
        {
            _componentManager = componentManager;
        }

        public IEnumerable<IGameObject> Query<T>() where T : class, IComponent
        {
            return _componentManager.GetComponents<T>().Select(c => c.Owner).Where(o => o != null)!;
        }

        public IEnumerable<IGameObject> Query(params Type[] componentTypes)
        {
            if (componentTypes == null || componentTypes.Length == 0)
                return Enumerable.Empty<IGameObject>();

            // Find the smallest set to start with
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
            // Reflection-based access to the generic GetComponents method
            var method = _componentManager.GetType().GetMethod("GetComponents")?.MakeGenericMethod(t);
            if (method == null) return 0;
            var results = method.Invoke(_componentManager, null) as System.Collections.IEnumerable;
            int count = 0;
            if (results != null) foreach (var _ in results) count++;
            return count;
        }

        private IEnumerable<IGameObject> GetOwners(Type t)
        {
            var method = _componentManager.GetType().GetMethod("GetComponents")?.MakeGenericMethod(t);
            if (method == null) return Enumerable.Empty<IGameObject>();
            var results = method.Invoke(_componentManager, null) as System.Collections.IEnumerable;
            if (results == null) return Enumerable.Empty<IGameObject>();
            return results.Cast<IComponent>().Select(c => c.Owner).Where(o => o != null)!;
        }
    }
}
