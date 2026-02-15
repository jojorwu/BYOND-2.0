using System.Collections.Concurrent;
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
        private readonly IComponentQueryService _componentQuery;
        private readonly System.IServiceProvider _serviceProvider;
        private bool _isDirty = true;

        public SystemManager(ISystemRegistry registry, IProfilingService profilingService, IJobSystem jobSystem, IComponentQueryService componentQuery, System.IServiceProvider serviceProvider)
        {
            _registry = registry;
            _profilingService = profilingService;
            _jobSystem = jobSystem;
            _componentQuery = componentQuery;
            _serviceProvider = serviceProvider;
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
                // Find systems whose dependencies are met
                var readySystems = remaining
                    .Where(s => s.Dependencies.All(d => completedNames.Contains(d)))
                    .ToList();

                if (readySystems.Count == 0)
                {
                    // Fallback for circular dependencies: sort by priority if no layer can be formed
                    var fallbackLayer = remaining.OrderByDescending(s => s.Priority).ToList();
                    layers.Add(fallbackLayer);
                    break;
                }

                // Further refine readySystems into sub-layers based on resource conflicts
                var subLayers = ResolveResourceConflicts(readySystems);
                layers.AddRange(subLayers);

                foreach (var system in readySystems)
                {
                    remaining.Remove(system);
                    completedNames.Add(system.Name);
                }
            }

            return layers;
        }

        private List<List<ISystem>> ResolveResourceConflicts(List<ISystem> systems)
        {
            var subLayers = new List<List<ISystem>>();
            var remaining = new List<ISystem>(systems);

            while (remaining.Count > 0)
            {
                var currentSubLayer = new List<ISystem>();
                var lockedForRead = new HashSet<System.Type>();
                var lockedForWrite = new HashSet<System.Type>();

                for (int i = 0; i < remaining.Count; i++)
                {
                    var system = remaining[i];
                    bool hasConflict = false;

                    foreach (var res in system.WriteResources)
                    {
                        if (lockedForWrite.Contains(res) || lockedForRead.Contains(res))
                        {
                            hasConflict = true;
                            break;
                        }
                    }

                    if (!hasConflict)
                    {
                        foreach (var res in system.ReadResources)
                        {
                            if (lockedForWrite.Contains(res))
                            {
                                hasConflict = true;
                                break;
                            }
                        }
                    }

                    if (!hasConflict)
                    {
                        currentSubLayer.Add(system);
                        foreach (var res in system.WriteResources) lockedForWrite.Add(res);
                        foreach (var res in system.ReadResources) lockedForRead.Add(res);
                        remaining.RemoveAt(i);
                        i--;
                    }
                }

                if (currentSubLayer.Count > 0)
                {
                    subLayers.Add(currentSubLayer);
                }
                else
                {
                    // Should not happen if logic is correct, but safety break
                    subLayers.Add(remaining.ToList());
                    remaining.Clear();
                }
            }

            return subLayers;
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
                    var ecbs = new ConcurrentBag<IEntityCommandBuffer>();

                    if (layer.Count == 1)
                    {
                        var ecb = (IEntityCommandBuffer)_serviceProvider.GetService(typeof(IEntityCommandBuffer))!;
                        ecbs.Add(ecb);
                        ExecuteSystem(layer[0], ecb);
                    }
                    else
                    {
                        await _jobSystem.ForEachAsync(layer, system =>
                        {
                            var ecb = (IEntityCommandBuffer)_serviceProvider.GetService(typeof(IEntityCommandBuffer))!;
                            ecbs.Add(ecb);
                            ExecuteSystem(system, ecb);
                        });
                    }

                    // Await jobs created by this layer before moving to the next
                    await _jobSystem.CompleteAllAsync();

                    // Synchronization Point: Playback all ECBs from this layer
                    using (_profilingService.Measure("SystemManager.ECBPlayback"))
                    {
                        foreach (var ecb in ecbs)
                        {
                            ecb.Playback();
                        }
                    }
                }

                // Post-Tick Phase
                using (_profilingService.Measure("SystemManager.PostTick"))
                {
                    await _jobSystem.ForEachAsync(enabledSystems, s => s.PostTick());
                }
            }
        }

        private void ExecuteSystem(ISystem system, IEntityCommandBuffer ecb)
        {
            using (_profilingService.Measure($"System.{system.Name}"))
            {
                system.Tick(ecb);

                var jobs = system.CreateJobs();
                foreach (var job in jobs)
                {
                    _jobSystem.Schedule(job);
                }
            }
        }
    }
}
