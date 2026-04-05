using System.Diagnostics;
using Shared.Interfaces;
using Shared.Models;
using Shared.Services;
using Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Benchmarks;

public struct BenchDataComponent : IDataComponent
{
    public double X;
    public double Y;
    public double Speed;
}

public class BenchClassComponent : IComponent
{
    public IGameObject? Owner { get; set; }
    public bool Enabled { get; set; } = true;
    public bool IsDirty { get; set; }
    public double X;
    public double Y;
    public double Speed;

    public void SendMessage(IComponentMessage message) { }
    public void Reset() { }
}

public class PureEcsBenchmark
{
    private const int EntityCount = 100_000;
    private const int Iterations = 100;

    public static async Task Main(string[] args)
    {
        var bench = new PureEcsBenchmark();
        Console.WriteLine($"Running Pure ECS vs Class Component Benchmark ({EntityCount} entities, {Iterations} iterations)");

        await bench.RunClassBenchmark();
        await bench.RunStructBenchmark();
    }

    private async Task RunClassBenchmark()
    {
        var diagnosticBus = new MockDiagnosticBus();
        var archManager = new ArchetypeManager(NullLogger<ArchetypeManager>.Instance, diagnosticBus);
        var compManager = new ComponentManager(archManager);
        var profiling = new ProfilingService();
        var jobSystem = new JobSystem(NullLogger<JobSystem>.Instance, TimeProvider.System, diagnosticBus);
        var ecbPool = new ObjectPool<EntityCommandBuffer>(() => new EntityCommandBuffer(new ObjectFactory(null!), compManager, jobSystem));
        var queryService = new ComponentQueryService(compManager, archManager, TimeProvider.System, diagnosticBus, null);

        var system = new ClassBenchSystem();
        var systemManager = new SystemManager(new SystemRegistry(), new SystemExecutionPlanner(), profiling, jobSystem, ecbPool, archManager, new[] { system }, new NullLoggerFactory(), queryService, diagnosticBus);
        await ((IEngineService)systemManager).InitializeAsync();

        for (int i = 0; i < EntityCount; i++)
        {
            var obj = new GameObject();
            obj.SetComponentManager(compManager);
            obj.Initialize(null!, 0, 0, 0);
            archManager.AddEntity(obj);
            obj.AddComponent(new BenchClassComponent { Speed = 1.0 });
        }

        // Warmup
        await systemManager.TickAsync();

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++)
        {
            await systemManager.TickAsync();
        }
        sw.Stop();

        Console.WriteLine($"Class Component: {sw.ElapsedMilliseconds}ms ({(EntityCount * Iterations) / sw.Elapsed.TotalSeconds:N0} entities/sec)");
    }

    private async Task RunStructBenchmark()
    {
        var diagnosticBus = new MockDiagnosticBus();
        var archManager = new ArchetypeManager(NullLogger<ArchetypeManager>.Instance, diagnosticBus);
        var compManager = new ComponentManager(archManager);
        var profiling = new ProfilingService();
        var jobSystem = new JobSystem(NullLogger<JobSystem>.Instance, TimeProvider.System, diagnosticBus);
        var ecbPool = new ObjectPool<EntityCommandBuffer>(() => new EntityCommandBuffer(new ObjectFactory(null!), compManager, jobSystem));
        var queryService = new ComponentQueryService(compManager, archManager, TimeProvider.System, diagnosticBus, null);

        var system = new StructBenchSystem();
        var systemManager = new SystemManager(new SystemRegistry(), new SystemExecutionPlanner(), profiling, jobSystem, ecbPool, archManager, new[] { system }, new NullLoggerFactory(), queryService, diagnosticBus);
        await ((IEngineService)systemManager).InitializeAsync();

        ComponentIdRegistry.Register<BenchDataComponent>();

        for (int i = 0; i < EntityCount; i++)
        {
            var obj = new GameObject();
            obj.SetComponentManager(compManager);
            obj.Initialize(null!, 0, 0, 0);
            archManager.AddEntity(obj);
            obj.SetDataComponent(new BenchDataComponent { Speed = 1.0 });
        }

        // Warmup
        await systemManager.TickAsync();

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++)
        {
            await systemManager.TickAsync();
        }
        sw.Stop();

        Console.WriteLine($"Pure ECS (Struct): {sw.ElapsedMilliseconds}ms ({(EntityCount * Iterations) / sw.Elapsed.TotalSeconds:N0} entities/sec)");
    }
}

public class ClassBenchSystem : BaseSystem
{
    [Shared.Attributes.Query]
    private EntityQuery<BenchClassComponent> _query;

    public override void Tick(IEntityCommandBuffer ecb) { }

    public override ValueTask TickAsync<T>(ArchetypeChunk<T> chunk, IEntityCommandBuffer ecb)
    {
        if (typeof(T) == typeof(BenchClassComponent))
        {
            var comps = ((ArchetypeChunk<BenchClassComponent>)(object)chunk).ComponentsSpan;
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                c.X += c.Speed;
                c.Y += c.Speed;
            }
        }
        return ValueTask.CompletedTask;
    }
}

public class StructBenchSystem : BaseSystem
{
    [Shared.Attributes.Query]
    private EntityQuery<BenchDataComponent> _query;

    public override void Tick(IEntityCommandBuffer ecb) { }

    public override ValueTask TickAsync<T>(ArchetypeChunk<T> chunk, IEntityCommandBuffer ecb)
    {
        if (typeof(T) == typeof(BenchDataComponent))
        {
            var comps = ((ArchetypeChunk<BenchDataComponent>)(object)chunk).ComponentsMutableSpan;
            for (int i = 0; i < comps.Length; i++)
            {
                comps[i].X += comps[i].Speed;
                comps[i].Y += comps[i].Speed;
            }
        }
        return ValueTask.CompletedTask;
    }
}
