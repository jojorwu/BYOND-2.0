using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Shared;
using Shared.Interfaces;
using Shared.Services;
using Server;
using Core;
using Core.VM;
using Core.VM.Runtime;
using Core.VM.Procs;
using Robust.Shared.Maths;
using Shared.Enums;
using Core.Api;
using Shared.Api;

namespace Benchmarks;

public class ComplexOpsBenchmark
{
    public static async Task Main(string[] args)
    {
        if (args.Contains("--chunk-benchmark"))
        {
            await ChunkLoadingBenchmark.RunAsync();
            return;
        }

        Console.WriteLine("Starting BYOND 2.0 Complex Operations Benchmark...");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IComputeService, ComputeService>();
        services.AddSingleton<SpatialGrid>(sp => new SpatialGrid(Microsoft.Extensions.Logging.Abstractions.NullLogger<SpatialGrid>.Instance));
        services.AddSingleton<IGameState, GameState>();
        services.AddSingleton<IMap, Map>();
        services.AddSingleton<IRegionManager, Core.Regions.RegionManager>();
        services.AddSingleton<ISoundApi, SoundApi>();
        services.AddSingleton<IUdpServer>(new Moq.Mock<IUdpServer>().Object);
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new ServerSettings()));

        var provider = services.BuildServiceProvider();

        var compute = provider.GetRequiredService<IComputeService>();
        var grid = provider.GetRequiredService<SpatialGrid>();

        // Benchmark 1: SpatialGrid Movement (100,000 moves)
        RunGridMovementBenchmark(grid);

        // Benchmark 2: SIMD Distance Calculation (1,000,000 pairs)
        RunSimdDistanceBenchmark(compute);

        // Benchmark 3: DreamValue Comparison Stress (10,000,000 checks)
        RunComparisonBenchmark();

        // Benchmark 4: Recursive Call Stress (10,000 depth)
        RunRecursionBenchmark();

        // Benchmark 5: Spatial Range Query (1,000 calls, 100 range)
        RunRangeBenchmark(grid);

        // Benchmark 6: Sound Dispatch Stress (10,000 sounds)
        RunSoundBenchmark(provider);

        Console.WriteLine("\nBenchmark Complete.");
    }

    private static void RunSoundBenchmark(IServiceProvider provider)
    {
        var soundApi = provider.GetRequiredService<ISoundApi>();
        Console.Write("Executing 10,000 sound dispatches... ");
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            soundApi.Play("test.ogg", 100, 1, false);
        }
        sw.Stop();
        Console.WriteLine($"{sw.ElapsedMilliseconds}ms");
    }

    private static void RunGridMovementBenchmark(SpatialGrid grid)
    {
        var mobType = new ObjectType(1, "mob");
        var mobs = new GameObject[1000];
        for (int i = 0; i < 1000; i++)
        {
            mobs[i] = new GameObject(mobType);
            mobs[i].SetPosition(i, i, 1);
            grid.Add(mobs[i]);
        }

        Console.Write("Executing 100,000 grid movements... ");
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            foreach (var mob in mobs)
            {
                mob.SetPosition(mob.X + 1, mob.Y + 1, 1);
                grid.Update(mob, mob.X - 1, mob.Y - 1);
            }
        }
        sw.Stop();
        Console.WriteLine($"{sw.ElapsedMilliseconds}ms");
    }

    private static void RunSimdDistanceBenchmark(IComputeService compute)
    {
        int count = 1000000;
        var x1 = new long[count];
        var y1 = new long[count];
        var x2 = new long[count];
        var y2 = new long[count];
        var results = new double[count];

        var rand = new Random(42);
        for (int i = 0; i < count; i++)
        {
            x1[i] = rand.Next(0, 1000);
            y1[i] = rand.Next(0, 1000);
            x2[i] = rand.Next(0, 1000);
            y2[i] = rand.Next(0, 1000);
        }

        Console.Write("Executing 1,000,000 SIMD distance calculations... ");
        var sw = Stopwatch.StartNew();
        compute.CalculateDistancesSIMD(x1, y1, x2, y2, results);
        sw.Stop();
        Console.WriteLine($"{sw.ElapsedMilliseconds}ms");
    }

    private static void RunComparisonBenchmark()
    {
        var v1 = new DreamValue(123.456);
        var v2 = new DreamValue(123.456);
        var v3 = new DreamValue(456.789);
        bool sink = false;

        Console.Write("Executing 10,000,000 DreamValue comparisons... ");
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10000000; i++)
        {
            sink ^= (v1 == v2);
            sink ^= (v1 == v3);
        }
        sw.Stop();
        Console.WriteLine($"{sw.ElapsedMilliseconds}ms (Sink: {sink})");
    }

    private static void RunRangeBenchmark(SpatialGrid grid)
    {
        var gameState = new Moq.Mock<IGameState>();
        gameState.Setup(s => s.SpatialGrid).Returns(grid);
        gameState.Setup(s => s.ReadLock()).Returns(new Moq.Mock<IDisposable>().Object);

        var typeManager = new Moq.Mock<IObjectTypeManager>();
        var mapApi = new Moq.Mock<IMapApi>();

        var spatialApi = new SpatialQueryApi(gameState.Object, typeManager.Object, mapApi.Object);

        Console.Write("Executing 1,000 spatial range queries... ");
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            var results = spatialApi.Range(100, 500, 500, 1);
        }
        sw.Stop();
        Console.WriteLine($"{sw.ElapsedMilliseconds}ms");
    }

    private static void RunRecursionBenchmark()
    {
        var bc = new BytecodeBuffer();

        // if (arg0 <= 0) return 0
        bc.Add(Opcode.PushArgument, 0);
        bc.Add(Opcode.PushFloat, 0.0);
        bc.Add(Opcode.CompareLessThanOrEqual);
        var jumpToReturn = bc.AddPlaceholderJump(Opcode.JumpIfFalse);
        bc.Add(Opcode.PushFloat, 0.0);
        bc.Add(Opcode.Return);

        bc.FillPlaceholder(jumpToReturn);

        // return 1 + recurse(arg0 - 1)
        bc.Add(Opcode.PushFloat, 1.0);
        bc.Add(Opcode.PushArgument, 0);
        bc.Add(Opcode.PushFloat, 1.0);
        bc.Add(Opcode.Subtract);

        bc.Add(Opcode.Call);
        bc.Add((byte)DMReference.Type.GlobalProc);
        bc.Add(0); // globalIdx
        bc.Add(DMCallArgumentsType.FromStack);
        bc.Add(1); // delta
        bc.Add(0); // unused

        bc.Add(Opcode.Add);
        bc.Add(Opcode.Return);

        var settings = new ServerSettings { VmMaxInstructions = 1000000000 };
        var vm = new DreamVM(Microsoft.Extensions.Options.Options.Create(settings),
                             Microsoft.Extensions.Logging.Abstractions.NullLogger<DreamVM>.Instance,
                             new INativeProcProvider[] { new StandardNativeProcProvider() });

        var proc = new DreamProc("recurse", bc.ToArray(), new[] { "n" }, 0);
        vm.Context.AllProcs.Add(proc);

        Console.Write("Executing 10,000 recursive calls... ");
        var sw = Stopwatch.StartNew();
        var thread = new DreamThread(proc, vm.Context, 1000000);
        thread.Push(new DreamValue(10000.0)); // arg n
        thread.Run(1000000);
        sw.Stop();

        var result = thread.Pop();
        Console.WriteLine($"{sw.ElapsedMilliseconds}ms (Result: {result.AsDouble()})");
    }

    private class BytecodeBuffer
    {
        private List<byte> _data = new();
        public int PC => _data.Count;

        public void Add(Opcode op) => _data.Add((byte)op);
        public void Add(byte b) => _data.Add(b);
        public void Add(DMCallArgumentsType type) => _data.Add((byte)type);
        public void Add(Opcode op, double val) { _data.Add((byte)op); _data.AddRange(BitConverter.GetBytes(val)); }
        public void Add(Opcode op, int val) { _data.Add((byte)op); _data.AddRange(BitConverter.GetBytes(val)); }
        public void Add(int val) { _data.AddRange(BitConverter.GetBytes(val)); }
        public void Add(Opcode op, DMReference.Type type, int idx)
        {
            _data.Add((byte)op);
            _data.Add((byte)type);
            _data.AddRange(BitConverter.GetBytes(idx));
        }

        public int AddPlaceholderJump(Opcode op)
        {
            _data.Add((byte)op);
            int pos = _data.Count;
            _data.AddRange(new byte[4]);
            return pos;
        }

        public void FillPlaceholder(int pos)
        {
            byte[] target = BitConverter.GetBytes(_data.Count);
            for (int i = 0; i < 4; i++) _data[pos + i] = target[i];
        }

        public byte[] ToArray() => _data.ToArray();
    }
}
