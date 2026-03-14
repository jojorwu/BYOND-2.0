using System;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;
using Shared.Models;
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

        // Pre-fetch resource masks to avoid Type lookups and HashSet operations in the planning loop
        var systemResources = systems.ToDictionary(s => s, s =>
        {
            var readMask = new ResourceMask();
            var writeMask = new ResourceMask();
            foreach (var type in s.ReadResources)
            {
                int id = ComponentIdRegistry.GetId(type);
                if (!readMask.Supports(id))
                    throw new InvalidOperationException($"System '{s.Name}' uses component type '{type.Name}' with ID {id}, which exceeds the supported resource mask limit (512).");
                readMask.Set(id);
            }
            foreach (var type in s.WriteResources)
            {
                int id = ComponentIdRegistry.GetId(type);
                if (!writeMask.Supports(id))
                    throw new InvalidOperationException($"System '{s.Name}' uses component type '{type.Name}' with ID {id}, which exceeds the supported resource mask limit (512).");
                writeMask.Set(id);
            }
            return (Read: readMask, Write: writeMask);
        });

        while (remaining.Count > 0)
        {
            var currentSubLayer = new List<ISystem>();
            var lockedForRead = new ResourceMask();
            var lockedForWrite = new ResourceMask();

            for (int i = 0; i < remaining.Count; i++)
            {
                var system = remaining[i];
                var (readMask, writeMask) = systemResources[system];

                // Write access conflicts with existing read or write locks
                bool hasConflict = writeMask.Overlaps(lockedForWrite) || writeMask.Overlaps(lockedForRead);

                // Read access conflicts with existing write locks
                if (!hasConflict)
                {
                    hasConflict = readMask.Overlaps(lockedForWrite);
                }

                if (!hasConflict)
                {
                    currentSubLayer.Add(system);
                    // Update current sub-layer locks
                    // Note: We need a way to combine ResourceMasks, adding a simple OR pattern or updating masks
                    // Since ResourceMask is a struct with private fields, let's update it to support combination or do it here
                    Combine(ref lockedForWrite, writeMask);
                    Combine(ref lockedForRead, readMask);

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

    private static void Combine(ref ResourceMask target, ResourceMask source)
    {
        // Internal fields are private, so we need to add a method to ResourceMask
        target.UnionWith(source);
    }
}
