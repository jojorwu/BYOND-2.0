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
        // For now, we still use Priority as a primary dependency driver,
        // but this architecture allows for explicit per-service dependency registration in the future.
        var sorted = _services.OrderByDescending(s => s.Priority).ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            var service = sorted[i];
            _dependencies[service] = new List<IEngineService>();

            // Implicitly, services with lower priority depend on all services with higher priority
            for (int j = 0; j < i; j++)
            {
                _dependencies[service].Add(sorted[j]);
            }
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
}
