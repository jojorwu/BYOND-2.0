using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Services;

/// <summary>
/// Orchestrates the initialization of services based on a directed acyclic graph of dependencies.
/// </summary>
public class ServiceDependencyGraph
{
    private readonly List<IEngineService> _services;
    private readonly Dictionary<IEngineService, List<IEngineService>> _dependencies = new();

    public ServiceDependencyGraph(IEnumerable<IEngineService> services)
    {
        _services = services.ToList();
        BuildGraph();
    }

    private void BuildGraph()
    {
        var nameToService = new Dictionary<string, IEngineService>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in _services)
        {
            if (!string.IsNullOrEmpty(s.Name))
            {
                if (nameToService.ContainsKey(s.Name))
                {
                    // If multiple services have the same name, we can't reliably depend on them by name.
                    // This might happen if someone registers the same service twice under different interfaces.
                    continue;
                }
                nameToService[s.Name] = s;
            }
        }

        foreach (var service in _services)
        {
            var deps = new HashSet<IEngineService>();

            // Explicit dependencies
            foreach (var depName in service.Dependencies)
            {
                if (nameToService.TryGetValue(depName, out var dep))
                {
                    deps.Add(dep);
                }
            }

            // Priority-based dependency: only depend on services with strictly higher priority.
            // This allows services with the same priority to be truly parallel.
            foreach (var other in _services)
            {
                if (other.Priority > service.Priority)
                {
                    deps.Add(other);
                }
            }

            _dependencies[service] = deps.ToList();
        }
    }

    public async Task ExecuteParallelAsync(Func<IEngineService, Task> action)
    {
        var completed = new HashSet<IEngineService>();
        var remaining = new HashSet<IEngineService>(_services);
        var lockObj = new object();

        while (remaining.Count > 0)
        {
            List<IEngineService> ready;
            lock (lockObj)
            {
                ready = remaining
                    .Where(s => _dependencies[s].All(d => completed.Contains(d)))
                    .OrderByDescending(s => s.Priority)
                    .ToList();
            }

            if (ready.Count == 0 && remaining.Count > 0)
            {
                throw new InvalidOperationException("Circular dependency or stalled graph in service execution.");
            }

            var tasks = ready.Select(async s =>
            {
                try
                {
                    await action(s);
                }
                finally
                {
                    lock (lockObj)
                    {
                        completed.Add(s);
                        remaining.Remove(s);
                    }
                }
            });

            await Task.WhenAll(tasks);
        }
    }

    public async Task ShutdownParallelAsync(Func<IEngineService, Task> shutdownAction)
    {
        var completed = new HashSet<IEngineService>();
        var remaining = new HashSet<IEngineService>(_services);
        var lockObj = new object();

        while (remaining.Count > 0)
        {
            List<IEngineService> ready;
            lock (lockObj)
            {
                // A service is ready to shutdown if no OTHER remaining service depends on it
                ready = remaining.Where(s => !remaining.Any(other => other != s && _dependencies[other].Contains(s))).ToList();
            }

            if (ready.Count == 0 && remaining.Count > 0)
            {
                throw new InvalidOperationException("Circular dependency or stalled graph in service shutdown.");
            }

            var tasks = ready.Select(async s =>
            {
                await shutdownAction(s);
                lock (lockObj)
                {
                    completed.Add(s);
                    remaining.Remove(s);
                }
            });

            await Task.WhenAll(tasks);
        }
    }
}
