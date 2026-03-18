using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Shared;
using Shared.Enums;
using Server;
using System.Text.Json;
using Core;
using Core.VM;
using Core.VM.Runtime;
using Core.VM.Procs;

namespace Benchmarks;

public class MathBenchmark
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Starting BYOND 2.0 64-bit VM Math Logic Benchmark...");

        var settings = new ServerSettings { VmMaxInstructions = 1000000000 };
        var vm = new DreamVM(Microsoft.Extensions.Options.Options.Create(settings),
                             Microsoft.Extensions.Logging.Abstractions.NullLogger<DreamVM>.Instance,
                             new INativeProcProvider[] {
                                 new MathNativeProcProvider(),
                                 new SpatialNativeProcProvider(),
                                 new SystemNativeProcProvider()
                             });

        // Benchmark 1: Simple Addition Loop (10,000,000 iterations)
        RunAdditionBenchmark(vm);

        // Benchmark 2: Complex Expression Loop (1,000,000 iterations)
        RunComplexMathBenchmark(vm);

        // Benchmark 3: Bitwise Operations (1,000,000 iterations)
        RunBitwiseBenchmark(vm);

        // Benchmark 4: List Indexing (1,000,000 iterations)
        RunIndexingBenchmark(vm);

        Console.WriteLine("\nBenchmark Complete.");
    }

    private static void RunAdditionBenchmark(DreamVM vm)
    {
        var bc = new BytecodeBuffer();

        // i = 0
        bc.Add(Opcode.PushFloat, 0.0);
        bc.Add(Opcode.AssignLocal, 0);
        bc.Add(Opcode.Pop);

        int loopStart = bc.PC;
        bc.Add(Opcode.PushLocal, 0);
        bc.Add(Opcode.PushFloat, 10000000.0);
        bc.Add(Opcode.CompareLessThan);

        var jumpToEnd = bc.AddPlaceholderJump(Opcode.JumpIfFalse);

        // i++
        bc.Add(Opcode.Increment, DMReference.Type.Local, 0);
        bc.Add(Opcode.Pop);

        bc.AddJump(Opcode.Jump, loopStart);
        bc.FillPlaceholder(jumpToEnd);

        bc.Add(Opcode.PushNull);
        bc.Add(Opcode.Return);

        var proc = new DreamProc("bench_add", bc.ToArray(), Array.Empty<string>(), 1);

        Console.Write("Executing 10,000,000 additions... ");
        var sw = Stopwatch.StartNew();
        var thread = new DreamThread(proc, vm.Context, 200000000);
        thread.Run(200000000);
        sw.Stop();
        Console.WriteLine($"{sw.ElapsedMilliseconds}ms ({(10000000.0 / sw.Elapsed.TotalSeconds) / 1000000:F2} Mops/s)");
    }

    private static void RunComplexMathBenchmark(DreamVM vm)
    {
        var bc = new BytecodeBuffer();

        // i = 0, x = 1.0
        bc.Add(Opcode.PushFloat, 0.0);
        bc.Add(Opcode.AssignLocal, 0);
        bc.Add(Opcode.Pop);
        bc.Add(Opcode.PushFloat, 1.0);
        bc.Add(Opcode.AssignLocal, 1);
        bc.Add(Opcode.Pop);

        int loopStart = bc.PC;
        bc.Add(Opcode.PushLocal, 0);
        bc.Add(Opcode.PushFloat, 1000000.0);
        bc.Add(Opcode.CompareLessThan);
        var jumpToEnd = bc.AddPlaceholderJump(Opcode.JumpIfFalse);

        // x = (x + 10) * 2 / 3.14
        bc.Add(Opcode.PushLocal, 1);
        bc.Add(Opcode.PushFloat, 10.0);
        bc.Add(Opcode.Add);
        bc.Add(Opcode.PushFloat, 2.0);
        bc.Add(Opcode.Multiply);
        bc.Add(Opcode.PushFloat, 3.14);
        bc.Add(Opcode.Divide);
        bc.Add(Opcode.AssignLocal, 1);
        bc.Add(Opcode.Pop);

        // i++
        bc.Add(Opcode.Increment, DMReference.Type.Local, 0);
        bc.Add(Opcode.Pop);

        bc.AddJump(Opcode.Jump, loopStart);
        bc.FillPlaceholder(jumpToEnd);

        bc.Add(Opcode.PushNull);
        bc.Add(Opcode.Return);

        var proc = new DreamProc("bench_complex", bc.ToArray(), Array.Empty<string>(), 2);

        Console.Write("Executing 1,000,000 complex expressions... ");
        var sw = Stopwatch.StartNew();
        var thread = new DreamThread(proc, vm.Context, 200000000);
        thread.Run(200000000);
        sw.Stop();
        Console.WriteLine($"{sw.ElapsedMilliseconds}ms");
    }

    private static void RunBitwiseBenchmark(DreamVM vm)
    {
        var bc = new BytecodeBuffer();

        // i = 0, x = 1.0
        bc.Add(Opcode.PushFloat, 0.0);
        bc.Add(Opcode.AssignLocal, 0);
        bc.Add(Opcode.Pop);
        bc.Add(Opcode.PushFloat, 1.0);
        bc.Add(Opcode.AssignLocal, 1);
        bc.Add(Opcode.Pop);

        int loopStart = bc.PC;
        bc.Add(Opcode.PushLocal, 0);
        bc.Add(Opcode.PushFloat, 1000000.0);
        bc.Add(Opcode.CompareLessThan);
        var jumpToEnd = bc.AddPlaceholderJump(Opcode.JumpIfFalse);

        // x = (x << 2) ^ 0xAAAAAAAA
        bc.Add(Opcode.PushLocal, 1);
        bc.Add(Opcode.PushFloat, 2.0);
        bc.Add(Opcode.BitShiftLeft);
        bc.Add(Opcode.PushFloat, (double)0xAAAAAAAA);
        bc.Add(Opcode.BitXor);
        bc.Add(Opcode.AssignLocal, 1);
        bc.Add(Opcode.Pop);

        // i++
        bc.Add(Opcode.Increment, DMReference.Type.Local, 0);
        bc.Add(Opcode.Pop);

        bc.AddJump(Opcode.Jump, loopStart);
        bc.FillPlaceholder(jumpToEnd);

        bc.Add(Opcode.PushNull);
        bc.Add(Opcode.Return);

        var proc = new DreamProc("bench_bitwise", bc.ToArray(), Array.Empty<string>(), 2);

        Console.Write("Executing 1,000,000 bitwise operations... ");
        var sw = Stopwatch.StartNew();
        var thread = new DreamThread(proc, vm.Context, 200000000);
        thread.Run(200000000);
        sw.Stop();
        Console.WriteLine($"{sw.ElapsedMilliseconds}ms");
    }

    private static void RunIndexingBenchmark(DreamVM vm)
    {
        vm.Context.ListType = new ObjectType(0, "/list");
        var list = new DreamList(vm.Context.ListType);
        for(int j=0; j<100; j++) list.AddValue(new DreamValue(j));
        vm.Context.SetGlobal(0, new DreamValue(list));

        var bc = new BytecodeBuffer();

        // i = 0
        bc.Add(Opcode.PushFloat, 0.0);
        bc.Add(Opcode.AssignLocal, 0);
        bc.Add(Opcode.Pop);

        int loopStart = bc.PC;
        bc.Add(Opcode.PushLocal, 0);
        bc.Add(Opcode.PushFloat, 1000000.0);
        bc.Add(Opcode.CompareLessThan);
        var jumpToEnd = bc.AddPlaceholderJump(Opcode.JumpIfFalse);

        // x = list[i % 100 + 1]
        bc.Add(Opcode.PushReferenceValue, DMReference.Type.Global, 0);
        bc.Add(Opcode.PushLocal, 0);
        bc.Add(Opcode.PushFloat, 100.0);
        bc.Add(Opcode.Modulus);
        bc.Add(Opcode.PushFloat, 1.0);
        bc.Add(Opcode.Add);
        bc.Add(Opcode.DereferenceIndex);
        bc.Add(Opcode.Pop);

        // i++
        bc.Add(Opcode.Increment, DMReference.Type.Local, 0);
        bc.Add(Opcode.Pop);

        bc.AddJump(Opcode.Jump, loopStart);
        bc.FillPlaceholder(jumpToEnd);

        bc.Add(Opcode.PushNull);
        bc.Add(Opcode.Return);

        var proc = new DreamProc("bench_indexing", bc.ToArray(), Array.Empty<string>(), 1);

        Console.Write("Executing 1,000,000 list indexings... ");
        var sw = Stopwatch.StartNew();
        var thread = new DreamThread(proc, vm.Context, 200000000);
        thread.Run(200000000);
        sw.Stop();
        Console.WriteLine($"{sw.ElapsedMilliseconds}ms");
    }

    private class BytecodeBuffer
    {
        private List<byte> _data = new();
        public int PC => _data.Count;

        public void Add(Opcode op) => _data.Add((byte)op);
        public void Add(Opcode op, double val) { _data.Add((byte)op); _data.AddRange(BitConverter.GetBytes(val)); }
        public void Add(Opcode op, int val) { _data.Add((byte)op); _data.AddRange(BitConverter.GetBytes(val)); }
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

        public void AddJump(Opcode op, int target)
        {
            _data.Add((byte)op);
            _data.AddRange(BitConverter.GetBytes(target));
        }

        public void FillPlaceholder(int pos)
        {
            byte[] target = BitConverter.GetBytes(_data.Count);
            for (int i = 0; i < 4; i++) _data[pos + i] = target[i];
        }

        public byte[] ToArray() => _data.ToArray();
    }
}
