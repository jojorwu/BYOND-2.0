using System;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Utils;
using Shared.Interfaces;
using Shared.Networking.FieldHandlers;
using Core;

using Shared.Buffers;
namespace Benchmarks;

public class LargeObjectUpdateBenchmark
{
    public static void Main(string[] args)
    {
        Console.WriteLine("--- Large Object Update Benchmark ---");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDiagnosticBus, DiagnosticBus>();
        services.AddSingleton<Shared.IObjectTypeManager, Shared.Services.ObjectTypeManager>();

        services.AddSingleton<IArchetypeManager, ArchetypeManager>();
        services.AddSingleton<IComponentManager, ComponentManager>();
        services.AddSingleton<IObjectPool<GameObject>>(sp => new SharedPool<GameObject>(() => new GameObject()));
        services.AddSingleton<IEntityRegistry, EntityRegistry>();
        services.AddSingleton<Shared.Interfaces.IObjectFactory, Shared.Services.ObjectFactory>();

        services.AddSingleton<SpatialGrid>(sp => new SpatialGrid(Microsoft.Extensions.Logging.Abstractions.NullLogger<SpatialGrid>.Instance, TimeProvider.System, sp.GetRequiredService<IDiagnosticBus>()));
        services.AddSingleton<Shared.IGameState, Shared.GameState>();
        services.AddSingleton<ISnapshotSerializer, BitPackedSnapshotSerializer>();
        services.AddSingleton<INetworkFieldHandler, TransformFieldHandler>();

        var serviceProvider = services.BuildServiceProvider();

        var gameState = serviceProvider.GetRequiredService<Shared.IGameState>();
        var typeManager = serviceProvider.GetRequiredService<Shared.IObjectTypeManager>();
        var factory = serviceProvider.GetRequiredService<Shared.Interfaces.IObjectFactory>();

        var type = new Shared.ObjectType(1, "mob");
        typeManager.RegisterObjectType(type);

        const int ObjectCount = 100000;
        Console.WriteLine($"Spawning {ObjectCount} objects...");
        var objects = new List<Shared.IGameObject>(ObjectCount);
        for (int i = 0; i < ObjectCount; i++)
        {
            var obj = (Shared.GameObject)factory.Create(type);
            gameState.AddGameObject(obj);
            objects.Add(obj);
        }

        Console.WriteLine("Starting update & serialization stress test...");
        var sw = Stopwatch.StartNew();

        // Simulate updates on all objects
        for (int i = 0; i < objects.Count; i++)
        {
            var obj = objects[i];
            if (obj is GameObject g) {
                g.Version++;
                g.X = (g.X + 1) % 1000;
            }
        }

        var serializer = serviceProvider.GetRequiredService<ISnapshotSerializer>();
        byte[] buffer = new byte[128 * 1024 * 1024]; // 128MB buffer
        var writer = new BitWriter(buffer);

        try
        {
            serializer.SerializeBitPackedDelta(ref writer, objects, null);
            Console.WriteLine($"Serialized {ObjectCount} objects into {writer.BytesWritten / 1024 / 1024} MB");

            // Basic verification of serialized data by reading it back
            var reader = new BitReader(buffer);
            int count = 0;
            while(true) {
                long id = reader.ReadVarInt();
                if (id == 0) break;
                long version = reader.ReadVarInt();
                long mask = (long)reader.ReadBits(32); // Updated to 32-bit mask
                count++;
            }
            Console.WriteLine($"Verified {count} objects in serialized stream.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Serialization/Verification failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        sw.Stop();
        Console.WriteLine($"Total time: {sw.ElapsedMilliseconds}ms");
    }
}
