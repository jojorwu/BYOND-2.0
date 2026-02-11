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
        private readonly IProfilingService _profilingService;

        public SystemManager(IEnumerable<ISystem> systems, IProfilingService profilingService)
        {
            _systems = systems.ToList();
            _profilingService = profilingService;

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
            using (_profilingService.Measure("SystemManager.Tick"))
            {
                foreach (var group in _priorityGroups)
                {
                    if (group.Count == 1)
                    {
                        var system = group[0];
                        using (_profilingService.Measure($"System.{system.GetType().Name}"))
                        {
                            system.Tick();
                        }
                    }
                    else
                    {
                        // Execute systems with the same priority in parallel
                        Parallel.ForEach(group, system =>
                        {
                            using (_profilingService.Measure($"System.{system.GetType().Name}"))
                            {
                                system.Tick();
                            }
                        });
                    }
                }
            }
        }
    }
}
