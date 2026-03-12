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
        var nameToService = new Dictionary<string, IEngineService>();
        foreach (var s in _services)
        {
            if (s.Name != null)
                nameToService[s.Name] = s;
        }

        var sortedByPriority = _services.OrderByDescending(s => s.Priority).ToList();

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

            // Implicit priority-based dependencies:
            // Services with lower priority depend on all services with higher priority
            int myIndex = sortedByPriority.IndexOf(service);
            for (int i = 0; i < myIndex; i++)
            {
                deps.Add(sortedByPriority[i]);
            }

            _dependencies[service] = deps.ToList();
        }
    }

    public async Task InitializeParallelAsync(Func<IEngineService, Task> initAction)
    {
        var completed = new HashSet<IEngineService>();
        var remaining = new HashSet<IEngineService>(_services);
        var lockObj = new object();

        while (remaining.Count > 0)
        {
            List<IEngineService> ready;
            lock (lockObj)
            {
                ready = remaining.Where(s => _dependencies[s].All(d => completed.Contains(d))).ToList();
            }

            if (ready.Count == 0 && remaining.Count > 0)
            {
                throw new InvalidOperationException("Circular dependency or stalled graph in service initialization.");
            }

            var tasks = ready.Select(async s =>
            {
                await initAction(s);
                lock (lockObj)
                {
                    completed.Add(s);
                    remaining.Remove(s);
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
