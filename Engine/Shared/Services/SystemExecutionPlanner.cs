using System;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;
using Shared.Enums;

namespace Shared.Services;

public class SystemExecutionPlanner : ISystemExecutionPlanner
{
    public List<List<ISystem>>[] PlanExecution(IEnumerable<ISystem> systems, ExecutionPhase[] phases)
    {
        var phaseExecutionLayers = new List<List<ISystem>>[phases.Length];
        var allSystems = systems.Where(s => s.Enabled).ToList();

        for (int i = 0; i < phases.Length; i++)
        {
            var phase = phases[i];
            var systemsInPhase = allSystems.Where(s => s.Phase == phase).ToList();
            if (systemsInPhase.Count > 0)
            {
                phaseExecutionLayers[i] = CalculateExecutionLayers(systemsInPhase);
            }
        }

        return phaseExecutionLayers;
    }

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
            var readySystems = new List<ISystem>();
            foreach (var system in remaining)
            {
                bool dependenciesMet = true;
                foreach (var dep in system.Dependencies)
                {
                    if (systemList.Any(s => s.Name == dep || s.Group == dep))
                    {
                        if (!completedNames.Contains(dep) && !completedGroups.Contains(dep))
                        {
                            dependenciesMet = false;
                            break;
                        }
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
                throw new InvalidOperationException($"Circular dependency detected among systems: {names}");
            }

            var subLayers = ResolveResourceConflicts(readySystems);
            layers.AddRange(subLayers);

            foreach (var system in readySystems)
            {
                remaining.Remove(system);
                completedNames.Add(system.Name);

                if (system.Group != null)
                {
                    var members = groupSystems[system.Group];
                    if (members.All(m => completedNames.Contains(m.Name)))
                    {
                        completedGroups.Add(system.Group);
                    }
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
            var lockedForRead = new HashSet<Type>();
            var lockedForWrite = new HashSet<Type>();

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
                subLayers.Add(new List<ISystem>(remaining));
                remaining.Clear();
            }
        }

        return subLayers;
    }
}
