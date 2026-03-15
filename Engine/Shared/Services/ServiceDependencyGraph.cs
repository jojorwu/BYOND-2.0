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
        var typeToService = new Dictionary<Type, IEngineService>();
        foreach (var s in _services)
        {
            var type = s.GetType();
            typeToService[type] = s;

            foreach (var @interface in type.GetInterfaces())
            {
                if (!typeToService.ContainsKey(@interface))
                {
                    typeToService[@interface] = s;
                }
            }
        }

        foreach (var service in _services)
        {
            var deps = new HashSet<IEngineService>();

            // Explicit dependencies
            foreach (var depType in service.Dependencies)
            {
                if (typeToService.TryGetValue(depType, out var dep))
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
        var dependencyCounts = _services.ToDictionary(s => s, s => _dependencies[s].Count);
        var dependents = _services.ToDictionary(s => s, _ => new List<IEngineService>());
        foreach (var kvp in _dependencies)
        {
            foreach (var dep in kvp.Value)
            {
                dependents[dep].Add(kvp.Key);
            }
        }

        var ready = new Queue<IEngineService>(_services.Where(s => dependencyCounts[s] == 0).OrderByDescending(s => s.Priority));
        int processedCount = 0;
        var lockObj = new object();

        while (ready.Count > 0)
        {
            List<IEngineService> currentBatch;
            lock (lockObj)
            {
                currentBatch = ready.ToList();
                ready.Clear();
            }

            var tasks = currentBatch.Select(async s =>
            {
                try
                {
                    await action(s);
                    Interlocked.Increment(ref processedCount);

                    var nextBatch = new List<IEngineService>();
                    foreach (var dependent in dependents[s])
                    {
                        if (Interlocked.Decrement(ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrNullRef(dependencyCounts, dependent)) == 0)
                        {
                            nextBatch.Add(dependent);
                        }
                    }

                    if (nextBatch.Count > 0)
                    {
                        lock (lockObj)
                        {
                            foreach (var n in nextBatch.OrderByDescending(x => x.Priority)) ready.Enqueue(n);
                        }
                    }
                }
                catch (Exception)
                {
                    if (s.IsCritical) throw;
                }
            });

            await Task.WhenAll(tasks);
        }

        if (processedCount < _services.Count)
        {
            throw new InvalidOperationException("Circular dependency detected in service graph.");
        }
    }

    public async Task ShutdownParallelAsync(Func<IEngineService, Task> shutdownAction)
    {
        // Inverse graph for shutdown
        var dependencyCounts = _services.ToDictionary(s => s, s => 0);
        var dependents = _services.ToDictionary(s => s, s => _dependencies[s]);

        foreach (var s in _services)
        {
            foreach (var dep in _dependencies[s])
            {
                dependencyCounts[dep]++;
            }
        }

        var ready = new Queue<IEngineService>(_services.Where(s => dependencyCounts[s] == 0).OrderBy(s => s.Priority));
        int processedCount = 0;
        var lockObj = new object();

        while (ready.Count > 0)
        {
            List<IEngineService> currentBatch;
            lock (lockObj)
            {
                currentBatch = ready.ToList();
                ready.Clear();
            }

            var tasks = currentBatch.Select(async s =>
            {
                await shutdownAction(s);
                Interlocked.Increment(ref processedCount);

                var nextBatch = new List<IEngineService>();
                foreach (var dep in dependents[s])
                {
                    if (Interlocked.Decrement(ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrNullRef(dependencyCounts, dep)) == 0)
                    {
                        nextBatch.Add(dep);
                    }
                }

                if (nextBatch.Count > 0)
                {
                    lock (lockObj)
                    {
                        foreach (var n in nextBatch.OrderBy(x => x.Priority)) ready.Enqueue(n);
                    }
                }
            });

            await Task.WhenAll(tasks);
        }

        if (processedCount < _services.Count)
        {
            throw new InvalidOperationException("Circular dependency detected during service shutdown.");
        }
    }
}
