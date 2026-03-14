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
        var completedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var completedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Map system name and group to existing objects for faster lookups
        var systemNameMap = systemList.ToDictionary(s => s.Name, s => s, StringComparer.OrdinalIgnoreCase);
        var groupMap = systemList.Where(s => s.Group != null).ToLookup(s => s.Group!, StringComparer.OrdinalIgnoreCase);

        while (remaining.Count > 0)
        {
            var readySystems = new List<ISystem>();
            foreach (var system in remaining)
            {
                bool dependenciesMet = true;
                foreach (var dep in system.Dependencies)
                {
                    // Check if dependency exists in our current set of systems
                    bool depExists = systemNameMap.ContainsKey(dep) || groupMap.Contains(dep);
                    if (depExists && !completedNames.Contains(dep) && !completedGroups.Contains(dep))
                    {
                        dependenciesMet = false;
                        break;
                    }
                }

                if (dependenciesMet) readySystems.Add(system);
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
                    var members = groupMap[system.Group];
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

        // Pre-fetch resource arrays to avoid IEnumerable overhead in tight loops
        var systemResources = systems.ToDictionary(s => s, s => (Read: s.ReadResources.ToArray(), Write: s.WriteResources.ToArray()));

        while (remaining.Count > 0)
        {
            var currentSubLayer = new List<ISystem>();
            var lockedForRead = new HashSet<Type>();
            var lockedForWrite = new HashSet<Type>();

            for (int i = 0; i < remaining.Count; i++)
            {
                var system = remaining[i];
                var (readRes, writeRes) = systemResources[system];
                bool hasConflict = false;

                for (int j = 0; j < writeRes.Length; j++)
                {
                    var res = writeRes[j];
                    if (lockedForWrite.Contains(res) || lockedForRead.Contains(res))
                    {
                        hasConflict = true;
                        break;
                    }
                }

                if (!hasConflict)
                {
                    for (int j = 0; j < readRes.Length; j++)
                    {
                        var res = readRes[j];
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
                    for (int j = 0; j < writeRes.Length; j++) lockedForWrite.Add(writeRes[j]);
                    for (int j = 0; j < readRes.Length; j++) lockedForRead.Add(readRes[j]);
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
                // This shouldn't happen with correct dependency planning, but act as a safety net
                subLayers.Add(new List<ISystem>(remaining));
                remaining.Clear();
            }
        }

        return subLayers;
    }
}
