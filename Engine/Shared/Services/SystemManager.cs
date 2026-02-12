using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Services
{
    public interface ISystemManager
    {
        Task TickAsync();
    }

    public class SystemManager : ISystemManager
    {
        private readonly ISystemRegistry _registry;
        private List<List<ISystem>> _executionLayers;
        private readonly IProfilingService _profilingService;
        private readonly IJobSystem _jobSystem;
        private bool _isDirty = true;

        public SystemManager(ISystemRegistry registry, IProfilingService profilingService, IJobSystem jobSystem)
        {
            _registry = registry;
            _profilingService = profilingService;
            _jobSystem = jobSystem;
            _executionLayers = new List<List<ISystem>>();
        }

        public void MarkDirty() => _isDirty = true;

        private List<List<ISystem>> CalculateExecutionLayers(IEnumerable<ISystem> systems)
        {
            var layers = new List<List<ISystem>>();
            var remaining = new HashSet<ISystem>(systems);
            var completedNames = new HashSet<string>();

            while (remaining.Count > 0)
            {
                var currentLayer = remaining
                    .Where(s => s.Dependencies.All(d => completedNames.Contains(d)))
                    .ToList();

                if (currentLayer.Count == 0)
                {
                    // Fallback for circular dependencies: sort by priority if no layer can be formed
                    var fallbackLayer = remaining.OrderByDescending(s => s.Priority).ToList();
                    layers.Add(fallbackLayer);
                    break;
                }

                layers.Add(currentLayer);
                foreach (var system in currentLayer)
                {
                    remaining.Remove(system);
                    completedNames.Add(system.Name);
                }
            }

            return layers;
        }

        public async Task TickAsync()
        {
            if (_isDirty)
            {
                _executionLayers = CalculateExecutionLayers(_registry.GetSystems().Where(s => s.Enabled));
                _isDirty = false;
            }

            var enabledSystems = _registry.GetSystems().Where(s => s.Enabled).ToList();

            using (_profilingService.Measure("SystemManager.Tick"))
            {
                // Pre-Tick Phase
                using (_profilingService.Measure("SystemManager.PreTick"))
                {
                    await _jobSystem.ForEachAsync(enabledSystems, s => s.PreTick());
                }

                // Main Tick Phase (Layered)
                foreach (var layer in _executionLayers)
                {
                    if (layer.Count == 1)
                    {
                        ExecuteSystem(layer[0]);
                    }
                    else
                    {
                        await _jobSystem.ForEachAsync(layer, ExecuteSystem);
                    }

                    // Await jobs created by this layer before moving to the next
                    await _jobSystem.CompleteAllAsync();
                }

                // Post-Tick Phase
                using (_profilingService.Measure("SystemManager.PostTick"))
                {
                    await _jobSystem.ForEachAsync(enabledSystems, s => s.PostTick());
                }
            }
        }

        private void ExecuteSystem(ISystem system)
        {
            using (_profilingService.Measure($"System.{system.Name}"))
            {
                system.Tick();

                var jobs = system.CreateJobs();
                foreach (var job in jobs)
                {
                    _jobSystem.Schedule(job);
                }
            }
        }
    }
}
