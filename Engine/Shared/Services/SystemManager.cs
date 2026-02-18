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
        private readonly IObjectPool<EntityCommandBuffer> _ecbPool;
        private readonly IEnumerable<IShrinkable> _shrinkables;
        private bool _isDirty = true;

        public SystemManager(ISystemRegistry registry, IProfilingService profilingService, IJobSystem jobSystem, IObjectPool<EntityCommandBuffer> ecbPool, IEnumerable<IShrinkable> shrinkables, IEnumerable<ISystem> systems)
        {
            _registry = registry;
            _profilingService = profilingService;
            _jobSystem = jobSystem;
            _ecbPool = ecbPool;
            _shrinkables = shrinkables;
            _executionLayers = new List<List<ISystem>>();

            // Automatically register all systems discovered by DI
            foreach (var system in systems)
            {
                system.Initialize();
                _registry.Register(system);
            }
        }

        public void MarkDirty() => _isDirty = true;

        private List<List<ISystem>> CalculateExecutionLayers(IEnumerable<ISystem> systems)
        {
            var layers = new List<List<ISystem>>();
            var systemList = systems.ToList();
            if (systemList.Count == 0) return layers;

            var remaining = new HashSet<ISystem>(systemList);
            var completedNames = new HashSet<string>();
            var completedGroups = new HashSet<string>();

            // Pre-calculate group memberships for faster checks
            var groupSystems = systemList.Where(s => s.Group != null).ToLookup(s => s.Group!);

            while (remaining.Count > 0)
            {
                // Find systems whose dependencies are met (both system-level and group-level)
                var readySystems = new List<ISystem>();
                foreach (var system in remaining)
                {
                    bool dependenciesMet = true;
                    foreach (var dep in system.Dependencies)
                    {
                        if (!completedNames.Contains(dep) && !completedGroups.Contains(dep))
                        {
                            dependenciesMet = false;
                            break;
                        }
                    }

                    if (dependenciesMet)
                    {
                        readySystems.Add(system);
                    }
                }

                if (readySystems.Count == 0)
                {
                    var names = string.Join(", ", remaining.Select(s => s.Name));
                    throw new System.InvalidOperationException($"Circular dependency detected among systems: {names}");
                }

                // Further refine readySystems into sub-layers based on resource conflicts
                var subLayers = ResolveResourceConflicts(readySystems);
                layers.AddRange(subLayers);

                foreach (var system in readySystems)
                {
                    remaining.Remove(system);
                    completedNames.Add(system.Name);

                    // If all systems in a group are completed, mark group as completed
                    if (system.Group != null)
                    {
                        var members = groupSystems[system.Group];
                        bool allDone = true;
                        foreach (var member in members)
                        {
                            if (!completedNames.Contains(member.Name))
                            {
                                allDone = false;
                                break;
                            }
                        }
                        if (allDone) completedGroups.Add(system.Group);
                    }
                }
            }

            return layers;
        }

        private List<List<ISystem>> ResolveResourceConflicts(List<ISystem> systems)
        {
            if (systems.Count <= 1) return new List<List<ISystem>> { systems };

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
                    subLayers.Add(new List<ISystem>(remaining));
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
                    if (layer.Count == 1)
                    {
                        var system = layer[0];
                        var ecb = _ecbPool.Rent();
                        try
                        {
                            ExecuteSystem(system, ecb);

                            // Await jobs created by this system
                            await _jobSystem.CompleteAllAsync();

                            using (_profilingService.Measure("SystemManager.ECBPlayback"))
                            {
                                ecb.Playback();
                            }
                        }
                        finally
                        {
                            _ecbPool.Return(ecb);
                        }
                    }
                    else
                    {
                        var ecbs = new ConcurrentBag<EntityCommandBuffer>();
                        await _jobSystem.ForEachAsync(layer, system =>
                        {
                            var ecb = _ecbPool.Rent();
                            ecbs.Add(ecb);
                            ExecuteSystem(system, ecb);
                        });

                        // Await jobs created by this layer
                        await _jobSystem.CompleteAllAsync();

                        // Synchronization Point: Playback all ECBs from this layer
                        using (_profilingService.Measure("SystemManager.ECBPlayback"))
                        {
                            foreach (var ecb in ecbs)
                            {
                                ecb.Playback();
                                _ecbPool.Return(ecb);
                            }
                        }
                    }
                }

                // Post-Tick Phase
                using (_profilingService.Measure("SystemManager.PostTick"))
                {
                    await _jobSystem.ForEachAsync(enabledSystems, s => s.PostTick());
                }

                // Cleanup Phase: Reset all worker arenas and shrink all registered pools/caches
                using (_profilingService.Measure("SystemManager.Cleanup"))
                {
                    await _jobSystem.ResetAllArenasAsync();

                    foreach (var shrinkable in _shrinkables)
                    {
                        shrinkable.Shrink();
                    }
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
