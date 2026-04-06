using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;

namespace Benchmarks;

public class ArchetypeSoABenchmark
{
    public class DummyComponent : IComponent {
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

    public static void Main(string[] args)
    {
        Console.WriteLine("--- Archetype SoA Access Benchmark ---");

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

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IObjectFactory>();
        var archetypeManager = serviceProvider.GetRequiredService<IArchetypeManager>();
        var typeManager = serviceProvider.GetRequiredService<IObjectTypeManager>();
        var componentManager = serviceProvider.GetRequiredService<IComponentManager>();

        var type = new ObjectType(1, "test");
        typeManager.RegisterObjectType(type);

        const int EntityCount = 100000;
        var entities = new List<GameObject>(EntityCount);

        Console.WriteLine($"Spawning {EntityCount} entities with components...");
        for (int i = 0; i < EntityCount; i++)
        {
            var entity = factory.Create(type, i, i, 0);
            entity.AddComponent(new DummyComponent());
            entities.Add(entity);
        }

        var allArchetypes = archetypeManager.GetArchetypesWithComponents(typeof(DummyComponent));
        var archetype = allArchetypes.FirstOrDefault();

        if (archetype == null)
        {
            Console.WriteLine("Error: No archetype found.");
            return;
        }

        Console.WriteLine("Benchmarking SoA Coord Access vs Entity Property Access...");

        const int Iterations = 500;
        long totalSum = 0;

        // Warmup
        foreach (var chunk in archetype.GetChunks<DummyComponent>(1024))
        {
            var xs = chunk.XsSpan;
            for (int i = 0; i < xs.Length; i++) totalSum += xs[i];
        }

        var sw = Stopwatch.StartNew();
        for (int it = 0; it < Iterations; it++)
        {
            foreach (var chunk in archetype.GetChunks<DummyComponent>(1024))
            {
                var xs = chunk.XsSpan;
                var ys = chunk.YsSpan;
                for (int i = 0; i < xs.Length; i++)
                {
                    totalSum += xs[i] + ys[i];
                }
            }
        }
        sw.Stop();
        Console.WriteLine($"SoA Access: {sw.ElapsedMilliseconds}ms (Sum: {totalSum})");

        totalSum = 0;
        sw.Restart();
        for (int it = 0; it < Iterations; it++)
        {
            foreach (var chunk in archetype.GetChunks<DummyComponent>(1024))
            {
                var ents = chunk.EntitiesSpan;
                for (int i = 0; i < ents.Length; i++)
                {
                    totalSum += ents[i].X + ents[i].Y;
                }
            }
        }
        sw.Stop();
        Console.WriteLine($"Entity Property Access: {sw.ElapsedMilliseconds}ms (Sum: {totalSum})");
    }
}
