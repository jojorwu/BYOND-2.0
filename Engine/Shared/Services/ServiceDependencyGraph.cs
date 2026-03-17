using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        var ready = new ConcurrentQueue<IEngineService>(_services.Where(s => dependencyCounts[s] == 0).OrderByDescending(s => s.Priority));
        int processedCount = 0;
        var completionTcs = new TaskCompletionSource();
        int pendingCount = 0;

        async Task ProcessReadyAsync()
        {
            while (ready.TryDequeue(out var service))
            {
                Interlocked.Increment(ref pendingCount);
                try
                {
                    await action(service);
                    Interlocked.Increment(ref processedCount);

                    foreach (var dependent in dependents[service])
                    {
                        if (Interlocked.Decrement(ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrNullRef(dependencyCounts, dependent)) == 0)
                        {
                            ready.Enqueue(dependent);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (service.IsCritical)
                    {
                        completionTcs.TrySetException(ex);
                        return;
                    }
                }
                finally
                {
                    if (Interlocked.Decrement(ref pendingCount) == 0 && ready.IsEmpty)
                    {
                        completionTcs.TrySetResult();
                    }
                }
            }
        }

        if (_services.Count == 0) return;
        if (ready.IsEmpty) throw new InvalidOperationException("Circular dependency detected in service graph.");

        // Start multiple workers to ensure true parallelism
        int workerCount = Math.Min(_services.Count, Environment.ProcessorCount);
        var workers = new Task[workerCount];
        for (int i = 0; i < workerCount; i++)
        {
            workers[i] = Task.Run(ProcessReadyAsync);
        }

        await Task.WhenAny(completionTcs.Task, Task.WhenAll(workers));
        if (completionTcs.Task.IsFaulted) await completionTcs.Task;

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

        var ready = new ConcurrentQueue<IEngineService>(_services.Where(s => dependencyCounts[s] == 0).OrderBy(s => s.Priority));
        int processedCount = 0;
        var completionTcs = new TaskCompletionSource();
        int pendingCount = 0;

        async Task ProcessReadyAsync()
        {
            while (ready.TryDequeue(out var service))
            {
                Interlocked.Increment(ref pendingCount);
                try
                {
                    await shutdownAction(service);
                    Interlocked.Increment(ref processedCount);

                    foreach (var dep in dependents[service])
                    {
                        if (Interlocked.Decrement(ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrNullRef(dependencyCounts, dep)) == 0)
                        {
                            ready.Enqueue(dep);
                        }
                    }
                }
                finally
                {
                    if (Interlocked.Decrement(ref pendingCount) == 0 && ready.IsEmpty)
                    {
                        completionTcs.TrySetResult();
                    }
                }
            }
        }

        if (_services.Count == 0) return;
        if (ready.IsEmpty) throw new InvalidOperationException("Circular dependency detected during service shutdown.");

        int workerCount = Math.Min(_services.Count, Environment.ProcessorCount);
        var workers = new Task[workerCount];
        for (int i = 0; i < workerCount; i++)
        {
            workers[i] = Task.Run(ProcessReadyAsync);
        }

        await Task.WhenAny(completionTcs.Task, Task.WhenAll(workers));

        if (processedCount < _services.Count)
        {
            throw new InvalidOperationException("Circular dependency detected during service shutdown.");
        }
    }
}
