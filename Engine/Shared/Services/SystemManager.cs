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
        private readonly ISystemRegistry _registry;
        private List<List<ISystem>> _executionLayers;
        private readonly IProfilingService _profilingService;
        private bool _isDirty = true;

        public SystemManager(ISystemRegistry registry, IProfilingService profilingService)
        {
            _registry = registry;
            _profilingService = profilingService;
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

        public void Tick()
        {
            if (_isDirty)
            {
                _executionLayers = CalculateExecutionLayers(_registry.GetSystems().Where(s => s.Enabled));
                _isDirty = false;
            }

            using (_profilingService.Measure("SystemManager.Tick"))
            {
                foreach (var layer in _executionLayers)
                {
                    if (layer.Count == 1)
                    {
                        ExecuteSystem(layer[0]);
                    }
                    else
                    {
                        Parallel.ForEach(layer, ExecuteSystem);
                    }
                }
            }
        }

        private void ExecuteSystem(ISystem system)
        {
            using (_profilingService.Measure($"System.{system.Name}"))
            {
                system.Tick();
            }
        }
    }
}
