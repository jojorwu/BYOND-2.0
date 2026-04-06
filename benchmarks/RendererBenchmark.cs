using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Client.Graphics;
using Robust.Shared.Maths;
using Silk.NET.OpenGL;
using Moq;

namespace Benchmarks;

public class RendererBenchmark
{
    // Logic-only version of WorldRenderer for benchmarking without GL context
    private class LogicOnlyWorldRenderer : IDisposable
    {
        private readonly List<RenderItem> _renderObjectBuffer = new();
        private readonly List<RenderItem>[] _layerBuckets = Enumerable.Range(0, 32).Select(_ => new List<RenderItem>()).ToArray();

        private struct RenderItem
        {
            public Vector2 Position;
            public Box2 Uv;
            public Color Color;
            public float Layer;
        }

        public void RenderDynamicObjects(GameState currentState, Box2 cullRect)
        {
            // Clear buckets
            for (int i = 0; i < _layerBuckets.Length; i++) _layerBuckets[i].Clear();

            _renderObjectBuffer.Clear();

            // We'll simulate the SpatialGrid query and the SoA access logic
            currentState.SpatialGrid.QueryBox(new Box3l((long)cullRect.Left, (long)cullRect.Top, -100, (long)cullRect.Right, (long)cullRect.Bottom, 100), obj => {
                if (obj.Archetype is not Archetype arch) return;
                int idx = obj.ArchetypeIndex;

                double layer = arch.GetLayer(idx);
                if (Math.Abs(layer - 2.0f) < 0.001f) return;

                if (obj.X < cullRect.Left - 1 || obj.X > cullRect.Right + 1 ||
                    obj.Y < cullRect.Top - 1 || obj.Y > cullRect.Bottom + 1)
                {
                    return;
                }

                string icon = arch.GetIcon(idx);
                if (string.IsNullOrEmpty(icon)) return;

                // Simulate icon/dmi lookup (omitted for pure logic speed)
                var pos = new Vector2(arch.GetX(idx) * 32 + (float)arch.GetPixelX(idx), arch.GetY(idx) * 32 + (float)arch.GetPixelY(idx));
                var color = Color.FromHex(arch.GetColor(idx)).WithAlpha((float)arch.GetAlpha(idx) / 255.0f);

                int bucketIdx = Math.Clamp((int)layer, 0, _layerBuckets.Length - 1);
                _layerBuckets[bucketIdx].Add(new RenderItem {
                    Position = pos,
                    Uv = new Box2(0, 0, 1, 1),
                    Color = color,
                    Layer = (float)layer
                });
            });

            // Simulate draw dispatch
            int totalDrawn = 0;
            for (int i = 0; i < _layerBuckets.Length; i++)
            {
                totalDrawn += _layerBuckets[i].Count;
            }
        }

        public void Dispose() { }
    }

    public static void Main(string[] args)
    {
        Console.WriteLine("--- Renderer Performance Benchmark ---");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDiagnosticBus, MockDiagnosticBus>();
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

        var type = new ObjectType(1, "test");
        typeManager.RegisterObjectType(type);

        const int EntityCount = 50000;
        Console.WriteLine($"Spawning {EntityCount} entities...");
        for (int i = 0; i < EntityCount; i++)
        {
            var entity = factory.Create(type, i % 100, i / 100, 0);
            entity.Icon = "icons/test.dmi/state";
            entity.Layer = (i % 32);
            entity.Color = "#FFFFFF";
            entity.Alpha = 255;
        }

        var gameState = new GameState(new SpatialGrid(NullLogger<SpatialGrid>.Instance, TimeProvider.System, new MockDiagnosticBus()), archetypeManager);
        foreach (var arch in archetypeManager.GetArchetypesWithComponents(ReadOnlySpan<Type>.Empty))
        {
            arch.ForEachEntity(e => {
                if (e is GameObject g) gameState.AddGameObject(g);
            });
        }

        var iconCache = new Client.Assets.IconCache();

        using var worldRenderer = new LogicOnlyWorldRenderer();

        Console.WriteLine("Benchmarking Renderer Logic (Culling + SoA Access + Bucketing)...");

        var cullRect = new Box2(0, 0, 100, 100);

        // Warmup
        for (int i = 0; i < 10; i++)
        {
            worldRenderer.RenderDynamicObjects(gameState, cullRect);
        }

        const int Iterations = 200;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++)
        {
            worldRenderer.RenderDynamicObjects(gameState, cullRect);
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / Iterations;
        double fps = 1000.0 / avgMs;

        Console.WriteLine($"Average Render Time: {avgMs:F2}ms");
        Console.WriteLine($"Effective Throughput: {fps:F0} FPS (at {EntityCount} entities)");
        Console.WriteLine($"Entities processed per second: {(EntityCount * fps):N0}");
    }
}
