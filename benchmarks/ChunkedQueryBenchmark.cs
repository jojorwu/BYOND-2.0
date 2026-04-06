using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Services.Systems;

namespace Benchmarks;

public class ChunkedQueryBenchmark
{
    public class PositionComponent : IComponent {
        public IGameObject? Owner { get; set; }
        public bool Enabled { get; set; } = true;
        public bool IsDirty { get; set; }
        public void Initialize() {}
        public void Shutdown() {}
        public void BeginUpdate() {}
        public void CommitUpdate() {}
        public void OnMessage(IComponentMessage message) {}
        public void SendMessage(IComponentMessage message) {}
        public void Reset() { Owner = null; Enabled = true; IsDirty = false; }
    }

    public class MovementSystem : BaseSystem {
        public override string Name => "MovementSystem";
        [Shared.Attributes.Query]
        private EntityQuery<PositionComponent> _query = null!;

        public override ValueTask TickAsync<T>(ArchetypeChunk<T> chunk, IEntityCommandBuffer ecb) {
            if (chunk is ArchetypeChunk<PositionComponent> pChunk) {
                var xs = pChunk.XsSpan;
                var ys = pChunk.YsSpan;
                var dirs = pChunk.DirsSpan;

                // Simulation logic directly on SoA data
                for (int i = 0; i < pChunk.Count; i++) {
                    long x = xs[i] + (dirs[i] == 1 ? 1 : -1);
                }
            }
            return ValueTask.CompletedTask;
        }

        public override void Tick(IEntityCommandBuffer ecb) {}
    }

    public static async Task Main(string[] args)
    {
        Console.WriteLine("--- Chunked Query & SIMD Execution Benchmark ---");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDiagnosticBus, DiagnosticBus>();
        services.AddSingleton<IObjectTypeManager, ObjectTypeManager>();
        services.AddSingleton<IArchetypeManager, ArchetypeManager>();
        services.AddSingleton<IComponentManager, ComponentManager>();
        services.AddSingleton<IObjectPool<GameObject>>(sp => new SharedPool<GameObject>(() => new GameObject()));
        services.AddSingleton<IEntityRegistry, EntityRegistry>();
        services.AddSingleton<IObjectFactory, Shared.Services.ObjectFactory>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IComponentQueryService, ComponentQueryService>();
        services.AddSingleton<ISystemRegistry, SystemRegistry>();
        services.AddSingleton<ISystemExecutionPlanner, SystemExecutionPlanner>();
        services.AddSingleton<IJobSystem, JobSystem>();
        services.AddSingleton<IProfilingService, ProfilingService>();
        services.AddSingleton<IObjectPool<EntityCommandBuffer>>(sp => new SharedPool<EntityCommandBuffer>(() => new EntityCommandBuffer(sp.GetRequiredService<IObjectFactory>(), sp.GetRequiredService<IComponentManager>(), sp.GetRequiredService<IJobSystem>())));

        var movementSystem = new MovementSystem();
        var cullingSystem = new VisibilityCullingSystem(new ProfilingService());
        cullingSystem.ViewMinX = 0; cullingSystem.ViewMinY = 0;
        cullingSystem.ViewMaxX = 500; cullingSystem.ViewMaxY = 500;

        services.AddSingleton<ISystem>(movementSystem);
        services.AddSingleton<ISystem>(cullingSystem);
        services.AddSingleton<SystemManager>();

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IObjectFactory>();
        var typeManager = serviceProvider.GetRequiredService<IObjectTypeManager>();
        var systemManager = serviceProvider.GetRequiredService<SystemManager>();

        var type = new ObjectType(1, "mob");
        type.FinalizeVariables();
        typeManager.RegisterObjectType(type);

        const int EntityCount = 500000;
        Console.WriteLine($"Spawning {EntityCount} entities...");
        for (int i = 0; i < EntityCount; i++)
        {
            var entity = factory.Create(type, i % 1000, i % 1000, 0);
            entity.AddComponent(new PositionComponent());
        }

        await systemManager.InitializeAsync();

        Console.WriteLine("Benchmarking 100 Ticks of Chunked SoA + SIMD Culling...");

        const int Iterations = 100;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++)
        {
            await systemManager.TickAsync();
        }
        sw.Stop();

        Console.WriteLine($"Execution Time: {sw.ElapsedMilliseconds}ms ({(double)sw.ElapsedMilliseconds/Iterations}ms per tick)");
        Console.WriteLine($"Throughput: {EntityCount * Iterations / (sw.Elapsed.TotalSeconds * 1000000):F2} Million Entities/Sec");
    }
}
