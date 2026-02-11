using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Services
{
    public interface ISystemManager
    {
        void Tick();
    }

    public class SystemManager : ISystemManager
    {
        private readonly List<ISystem> _systems;
        private readonly List<List<ISystem>> _priorityGroups;

        public SystemManager(IEnumerable<ISystem> systems)
        {
            _systems = systems.ToList();

            // Group systems by priority and sort groups descending (higher priority first)
            _priorityGroups = _systems
                .Where(s => s.Enabled)
                .GroupBy(s => s.Priority)
                .OrderByDescending(g => g.Key)
                .Select(g => g.ToList())
                .ToList();
        }

        public void Tick()
        {
            foreach (var group in _priorityGroups)
            {
                if (group.Count == 1)
                {
                    group[0].Tick();
                }
                else
                {
                    // Execute systems with the same priority in parallel
                    Parallel.ForEach(group, system =>
                    {
                        system.Tick();
                    });
                }
            }
        }
    }
}
