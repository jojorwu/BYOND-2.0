using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Utils;
using Shared.Interfaces;
using Shared.Enums;
using Core;
using Core.VM.Procs;
using Core.VM.Runtime;
using Robust.Shared.Maths;

namespace Benchmarks;

public class AsyncApiBenchmark
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("--- Async API & Pathfinding Benchmark ---");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDiagnosticBus, DiagnosticBus>();
        services.AddSingleton<IObjectTypeManager, ObjectTypeManager>();
        services.AddSingleton<Shared.Interfaces.IObjectFactory, Shared.Services.ObjectFactory>();

        services.AddSingleton<IArchetypeManager, ArchetypeManager>();
        services.AddSingleton<IComponentManager, ComponentManager>();
        services.AddSingleton<IObjectPool<GameObject>>(sp => new SharedPool<GameObject>(() => new GameObject()));
        services.AddSingleton<IEntityRegistry, EntityRegistry>();

        services.AddSingleton<SpatialGrid>(sp => new SpatialGrid(Microsoft.Extensions.Logging.Abstractions.NullLogger<SpatialGrid>.Instance, TimeProvider.System, sp.GetRequiredService<IDiagnosticBus>()));
        services.AddSingleton<IGameState, GameState>();
        services.AddSingleton<IJobSystem, JobSystem>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IPathfindingService, PathfindingService>();

        var serviceProvider = services.BuildServiceProvider();

        var pathfinding = serviceProvider.GetRequiredService<IPathfindingService>();

        const int RequestCount = 1000;
        Console.WriteLine($"Running {RequestCount} parallel pathfinding requests (C# level)...");

        var sw = Stopwatch.StartNew();
        var tasks = new List<Task<List<Vector3l>?>>();

        for (int i = 0; i < RequestCount; i++)
        {
            var start = new Vector3l(0, 0, 0);
            var end = new Vector3l(50, 50, 0);
            tasks.Add(pathfinding.FindPathAsync(start, end));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        Console.WriteLine($"Completed {RequestCount} requests in {sw.ElapsedMilliseconds}ms (Avg: {(double)sw.ElapsedMilliseconds/RequestCount}ms)");

        // Verify VM integration logic
        Console.WriteLine("Verifying VM Async Suspension logic...");
        var vmContext = new DreamVMContext();
        vmContext.ObjectTypeManager = serviceProvider.GetRequiredService<IObjectTypeManager>();
        vmContext.ListType = new ObjectType(100, "list");

        var thread = new DreamThread();
        var dummyProc = new DreamProc("dummy", Array.Empty<byte>(), Array.Empty<string>(), 0);
        thread.Initialize(dummyProc, vmContext, 1000000);

        var pathResult = new List<Vector3l> { new Vector3l(1, 1, 0) };
        var taskToWait = Task.FromResult<object?>(pathResult);

        thread.SuspendUntil(taskToWait);
        if (thread.State != DreamThreadState.Suspended) throw new Exception("Thread should be suspended");

        bool resumed = thread.CheckSuspension();
        if (!resumed || thread.State != DreamThreadState.Running) throw new Exception("Thread should be resumed");

        var result = thread.Pop();
        Console.WriteLine($"VM Resume Result Type: {result.Type}");
        if (result.Type == DreamValueType.DreamObject && result.TryGetValue(out DreamObject? obj) && obj is DreamList list) {
            Console.WriteLine($"Path length: {list.Values.Count}, First point: {list.Values[0]}");
        }
    }
}
